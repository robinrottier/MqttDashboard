# Update Agent

This folder contains a small host-side update agent for on-demand updates of the Dockerized application.

Goal
- Allow the running web app to request an update (pull a new image and restart a service) without giving the app direct control of Docker.

Components
- `update-agent.sh` — Bash script that runs `docker compose pull` + `docker compose up -d` for a named service, or runs `containrrr/watchtower --run-once` for a named container.
- `agent.py` — Minimal Python Flask HTTP wrapper that listens on `127.0.0.1` and exposes `POST /update`. It validates the `X-Update-Token` header against the `UPDATE_AGENT_TOKEN` environment variable and invokes `update-agent.sh`.
- `agent.service` — Example `systemd` unit to run the agent on Linux (copy to `/etc/systemd/system/agent.service` and edit `WorkingDirectory` and `Environment` as needed).

Security
- The agent binds to `127.0.0.1` only. Do not expose it to the internet.
- Requests must include the `X-Update-Token` header matching the `UPDATE_AGENT_TOKEN` env var to be accepted.
- The Flask wrapper executes `update-agent.sh` located in the same directory. Keep the directory and token secure and run the service under a dedicated user if possible.

Usage

1. Copy files to the host machine and place them in e.g. `/opt/mqttdashboard/tools/update-agent`.
   Add some permissions:
       chmod +x *.sh

2. Set the secret token in the environment or systemd unit:
   `export UPDATE_AGENT_TOKEN=your-secret`

3. Start the agent (for testing):
   `python3 agent.py --host 127.0.0.1 --port 8080`

4. Example request (server-side call, not from untrusted client):

   POST http://127.0.0.1:8080/update
   Headers:
     X-Update-Token: <your-secret>
   Body (JSON):
     { "service": "mqttdashboard_webapp", "composeFile": "docker-compose.yml" }

   Or to run watchtower once:
     { "watchtowerContainer": "my-watchtower-container" }

Systemd
- cd /opt/mqttdashboard/tools/update-agent
- Edit `agent.service` and set `WorkingDirectory` to the agent folder and `Environment` to your token.
- Enable and start:
  sudo systemctl daemon-reload
  sudo systemctl enable --now ./agent.service

Notes
- This approach avoids granting Docker socket access to your application container. The app should call your server-side endpoint which in turn invokes this agent on the host.
- The update process runs `docker compose pull` followed by `docker compose up -d` for the requested service. Ensure your compose file and service name match what you pass in the request.
