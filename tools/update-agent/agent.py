#!/usr/bin/env python3
"""
Minimal local update agent HTTP wrapper.
Listens on 127.0.0.1 and accepts POST /update with JSON body.
Validates X-Update-Token header against UPDATE_AGENT_TOKEN env var.
Calls the `update-agent.sh` script in the same directory to perform the actual docker compose / watchtower work.

Usage:
  export UPDATE_AGENT_TOKEN=your-secret
  python3 agent.py --port 8080

Security:
- Bind to localhost only. Do not expose this to the internet.
- Keep the token secret and only allow trusted callers (the app server on the host).
"""

import os
import subprocess
import json
import argparse
from flask import Flask, request, jsonify, abort

app = Flask(__name__)

TOKEN = os.environ.get('UPDATE_AGENT_TOKEN')
SCRIPT_PATH = os.path.join(os.path.dirname(__file__), 'update-agent.sh')


def fail(msg, code=400):
    return jsonify({'error': msg}), code


@app.route('/update', methods=['POST'])
def update():
    # ensure request is from localhost
    remote = request.remote_addr
    if remote not in ('127.0.0.1', '::1'):
        return fail('Forbidden', 403)

    if TOKEN:
        provided = request.headers.get('X-Update-Token')
        if provided != TOKEN:
            return fail('Unauthorized', 401)

    try:
        payload = request.get_json(force=True)
    except Exception:
        return fail('Invalid JSON', 400)

    if not isinstance(payload, dict):
        return fail('JSON object required', 400)

    watchtower = payload.get('watchtowerContainer')
    service = payload.get('service')
    compose_file = payload.get('composeFile')
    workdir = payload.get('workdir') or '.'

    # Build command
    if watchtower:
        cmd = [SCRIPT_PATH, '', '', watchtower, workdir]
    else:
        if not service:
            return fail('service is required', 400)
        cmd = [SCRIPT_PATH, service, compose_file or '', '', workdir]

    # Ensure script is executable
    if not os.path.isfile(SCRIPT_PATH):
        return fail('update-agent.sh not found on host', 500)

    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, cwd=os.path.dirname(__file__), timeout=600)
        result = {
            'returncode': proc.returncode,
            'stdout': proc.stdout,
            'stderr': proc.stderr,
        }
        status = 200 if proc.returncode == 0 else 500
        return jsonify(result), status
    except subprocess.TimeoutExpired:
        return fail('update process timed out', 500)
    except Exception as ex:
        return fail(str(ex), 500)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Local update agent HTTP wrapper')
    parser.add_argument('--host', default='127.0.0.1', help='Host to bind (default 127.0.0.1)')
    parser.add_argument('--port', type=int, default=int(os.environ.get('UPDATE_AGENT_PORT', 8080)), help='Port to bind (default 8080)')
    args = parser.parse_args()
    # Warn if token not set
    if not TOKEN:
        print('WARNING: UPDATE_AGENT_TOKEN not set. Requests will not be authenticated.')
    app.run(host=args.host, port=args.port)
