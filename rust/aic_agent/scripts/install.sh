#!/bin/bash
# AI Consumption Tracker Agent - Linux Installation Script
# Run with sudo for system-wide install

set -e

SERVICE_NAME="aic-agent"
INSTALL_DIR="/usr/local/bin"
CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/ai-consumption-tracker"
DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/ai-consumption-tracker"
SYSTEMD_DIR="$HOME/.config/systemd/user"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}  AI Consumption Tracker Agent Setup${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

install_service() {
    local binary_path="$1"
    local is_system="$2"

    echo -e "${YELLOW}[1/5] Installing $SERVICE_NAME service...${NC}"

    # Create directories
    mkdir -p "$CONFIG_DIR"
    mkdir -p "$DATA_DIR"
    mkdir -p "$SYSTEMD_DIR"

    # Copy binary
    if [ -f "$binary_path" ]; then
        cp "$binary_path" "$INSTALL_DIR/$SERVICE_NAME"
        chmod +x "$INSTALL_DIR/$SERVICE_NAME"
        echo -e "      ${GREEN}Binary installed to $INSTALL_DIR/$SERVICE_NAME${NC}"
    else
        echo -e "      ${RED}ERROR: Binary not found at $binary_path${NC}"
        echo -e "      ${YELLOW}Build the agent first: cargo build --release -p aic_agent${NC}"
        exit 1
    fi

    # Create default config if not exists
    if [ ! -f "$CONFIG_DIR/agent.toml" ]; then
        cat > "$CONFIG_DIR/agent.toml" << EOF
# AI Consumption Tracker Agent Configuration
database_path = "$DATA_DIR/usage.db"
listen_address = "127.0.0.1:8080"
polling_enabled = true
polling_interval_seconds = 300
log_level = "info"
provider_timeout_seconds = 30
retention_days = 30
auto_start = false
EOF
        echo -e "      ${GREEN}Config created at $CONFIG_DIR/agent.toml${NC}"
    fi

    # Create systemd user service
    cat > "$SYSTEMD_DIR/$SERVICE_NAME.service" << EOF
[Unit]
Description=AI Consumption Tracker Collection Agent
Documentation=https://github.com/rygel/AIConsumptionTracker
After=network.target

[Service]
Type=simple
WorkingDirectory=$DATA_DIR
ExecStart=$INSTALL_DIR/$SERVICE_NAME start
Restart=on-failure
RestartSec=5
Environment="RUST_LOG=info"

[Install]
WantedBy=default.target
EOF

    echo -e ""
    echo -e "${YELLOW}[2/5] Enabling and starting service...${NC}"

    # Reload systemd, enable and start the service
    systemctl --user daemon-reload
    systemctl --user enable "$SERVICE_NAME.service"
    systemctl --user start "$SERVICE_NAME.service"

    sleep 2

    # Check status
    if systemctl --user is-active --quiet "$SERVICE_NAME.service"; then
        echo -e "      ${GREEN}Service is running${NC}"
    else
        echo -e "      ${YELLOW}WARNING: Service may not be running yet${NC}"
    fi

    echo -e ""
    echo -e "${YELLOW}[3/5] Testing API endpoint...${NC}"

    # Test API
    sleep 1
    if curl -s http://127.0.0.1:8080/health > /dev/null 2>&1; then
        echo -e "      ${GREEN}Agent API responding at http://127.0.0.1:8080${NC}"
    else
        echo -e "      ${YELLOW}API not responding yet (may need a moment)${NC}"
    fi

    echo -e ""
    echo -e "${CYAN}========================================${NC}"
    echo -e "${CYAN}  Installation Complete!${NC}"
    echo -e "${CYAN}========================================${NC}"
    echo ""
    echo -e "Service: $SERVICE_NAME"
    echo -e "API URL: http://127.0.0.1:8080"
    echo -e "Config:  $CONFIG_DIR/agent.toml"
    echo ""
    echo -e "Commands:"
    echo -e "  ${GREEN}systemctl --user start $SERVICE_NAME${NC}   - Start service"
    echo -e "  ${GREEN}systemctl --user stop $SERVICE_NAME${NC}    - Stop service"
    echo -e "  ${GREEN}systemctl --user status $SERVICE_NAME${NC}   - Check status"
    echo -e "  ${GREEN}journalctl --user -u $SERVICE_NAME${NC}      - View logs"
    echo ""
}

uninstall_service() {
    echo -e "${YELLOW}[1/3] Uninstalling $SERVICE_NAME...${NC}"

    systemctl --user stop "$SERVICE_NAME.service" 2>/dev/null || true
    systemctl --user disable "$SERVICE_NAME.service" 2>/dev/null || true
    rm -f "$INSTALL_DIR/$SERVICE_NAME"
    rm -f "$SYSTEMD_DIR/$SERVICE_NAME.service"

    systemctl --user daemon-reload

    echo -e "      ${GREEN}Service removed${NC}"
    echo -e ""
    echo -e "${GREEN}Done!${NC}"
}

show_help() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --binary PATH    Path to aic-agent binary (default: target/release/aic-agent)"
    echo "  --system         Install as system service (requires root)"
    echo "  --uninstall       Remove the service"
    echo "  --help           Show this help"
    echo ""
    echo "Examples:"
    echo "  $0                           # Install using default binary"
    echo "  $0 --binary ./target/release/aic-agent"
    echo "  $0 --uninstall               # Remove the service"
}

# Parse arguments
BINARY_PATH="target/release/aic-agent"
IS_SYSTEM=false
ACTION="install"

while [[ $# -gt 0 ]]; do
    case $1 in
        --binary)
            BINARY_PATH="$2"
            shift 2
            ;;
        --system)
            IS_SYSTEM=true
            shift
            ;;
        --uninstall)
            ACTION="uninstall"
            shift
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

case $ACTION in
    install)
        install_service "$BINARY_PATH" "$IS_SYSTEM"
        ;;
    uninstall)
        uninstall_service
        ;;
esac
