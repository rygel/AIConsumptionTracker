/*
 * AI Consumption Tracker - Settings Window JavaScript (settings.html)
 */

// Settings window state
let configs = [];
let usageData = [];
let prefs = {};
let settingsChanged = false;
let freshDataArrived = false;
let githubAuthStatus = { is_authenticated: false, username: '', token_invalid: false };
let cachedAgentPort = 8080;
let currentHistorySubTab = 'usage';

/**
 * Initialize settings window
 */
async function initSettingsWindow() {
    console.log('=== SETTINGS.HTML SCRIPT START ===');
    console.log('window.__TAURI__ available:', typeof window.__TAURI__ !== 'undefined');
    console.log('invoke function available:', !!window.AicUtils?.invoke);

    setupTabs();
    setupCloseButton();
    setupInputListeners();
    setupAlwaysOnTop();
    setupPrivacyModeListener();
    loadPrivacyMode();
    await loadSettings();
    await loadAppVersion();
    updateFontPreview();
    setupEventListeners();
}

/**
 * Setup tab navigation
 */
function setupTabs() {
    document.querySelectorAll('.tab').forEach(tab => {
        tab.addEventListener('click', () => switchTab(tab.dataset.tab));
    });
}

/**
 * Setup close button
 */
function setupCloseButton() {
    document.getElementById('closeBtn').addEventListener('click', closeSettings);
}

/**
 * Setup input change listeners
 */
function setupInputListeners() {
    document.querySelectorAll('.form-input, .form-select, input[type="checkbox"]').forEach(el => {
        el.addEventListener('change', () => settingsChanged = true);
        el.addEventListener('input', () => settingsChanged = true);
    });

    document.getElementById('fontBold').addEventListener('change', updateFontPreview);
    document.getElementById('fontItalic').addEventListener('change', updateFontPreview);
    document.getElementById('fontFamily').addEventListener('change', updateFontPreview);
    document.getElementById('fontSize').addEventListener('input', updateFontPreview);
}

/**
 * Setup always on top toggle
 */
function setupAlwaysOnTop() {
    document.getElementById('alwaysOnTop').addEventListener('change', async (e) => {
        console.log('Always on Top checkbox changed:', e.target.checked);
        if (window.AicUtils?.invoke) {
            try {
                await window.AicUtils.invoke('toggle_always_on_top', { enabled: e.target.checked });
                console.log('Always on Top toggled successfully');
            } catch (err) {
                console.error('Failed to toggle always on top:', err);
            }
        }
    });
}

/**
 * Setup privacy mode
 */
function setupPrivacyModeListener() {
    window.AicUtils?.setupPrivacyModeListener();
}

/**
 * Load privacy mode
 */
function loadPrivacyMode() {
    window.privacyMode = window.AicUtils?.loadPrivacyMode() || false;
    const btn = document.getElementById('privacyToggleBtn');
    if (btn) {
        btn.classList.toggle('active', window.privacyMode);
        btn.title = window.privacyMode ? 'Privacy Mode On (Click to Show)' : 'Privacy Mode Off (Click to Hide)';
    }
    applyPrivacyMode();
}

/**
 * Setup event listeners for Tauri events
 */
function setupEventListeners() {
    if (window.__TAURI__?.event) {
        // Settings window shown
        window.__TAURI__.event.listen('settings-window-shown', async (event) => {
            console.log('=== Settings window shown event received ===', event);
            await loadSettings();
            await loadAppVersion();
        });

        // Window focus
        window.__TAURI__.event.listen('tauri://focus', async () => {
            console.log('Settings window focused - syncing badge state');
            await updateCachedBadge();
        });

        // Agent ready
        window.__TAURI__.event.listen('agent-ready', async (event) => {
            console.log('=== Agent ready event received ===', event);
            await loadSettings();
            await loadAppVersion();
        });

        // Data status changed
        window.__TAURI__.event.listen('data-status-changed', async (event) => {
            console.log('[CACHE] Settings received data-status-changed event:', event.payload);
            await updateCachedBadge();
        });
    }

    // F12 for DevTools
    document.addEventListener('keydown', async (e) => {
        if (e.key === 'F12') {
            console.log('F12 pressed - attempting to open DevTools');
            try {
                if (window.__TAURI__?.core?.invoke) {
                    await window.__TAURI__.core.invoke('open_devtools');
                } else if (window.__TAURI__?.invoke) {
                    await window.__TAURI__.invoke('open_devtools');
                }
            } catch (err) {
                console.error('Failed to open DevTools:', err);
            }
        }
    });

    // Escape to close
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            console.log('Escape pressed - closing settings window');
            closeSettings();
        }
    });
}

/**
 * Switch tabs
 */
function switchTab(tabName) {
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));

    document.querySelector(`[data-tab="${tabName}"]`).classList.add('active');
    document.getElementById(`${tabName}-panel`).classList.add('active');

    if (tabName === 'history') {
        refreshHistory();
    }
}

/**
 * Switch history subtab
 */
function switchHistorySubTab(subTab) {
    currentHistorySubTab = subTab;
    document.getElementById('usage-subtab-btn').classList.toggle('active', subTab === 'usage');
    document.getElementById('agent-subtab-btn').classList.toggle('active', subTab === 'agent');
    document.getElementById('logs-subtab-btn').classList.toggle('active', subTab === 'logs');

    document.getElementById('usage-history-subpanel').style.display = subTab === 'usage' ? 'block' : 'none';
    document.getElementById('agent-history-subpanel').style.display = subTab === 'agent' ? 'block' : 'none';
    document.getElementById('logs-history-subpanel').style.display = subTab === 'logs' ? 'block' : 'none';

    refreshHistory();
}

/**
 * Refresh history based on current subtab
 */
async function refreshHistory() {
    if (currentHistorySubTab === 'usage') {
        await loadUsageHistoryAsync();
    } else if (currentHistorySubTab === 'agent') {
        await loadAgentHistoryAsync();
    } else {
        await loadRawLogsAsync();
    }
}

/**
 * Load usage history
 */
async function loadUsageHistoryAsync() {
    const container = document.getElementById('history-list');
    container.innerHTML = '<div class="loading">Loading usage history...</div>';

    try {
        const history = await window.AicUtils.invoke('get_historical_usage_from_agent', { limit: 50 });
        if (!history || history.length === 0) {
            container.innerHTML = '<div class="loading">No history found.</div>';
            return;
        }

        let html = `<table class="history-table">
            <thead>
                <tr>
                    <th>Time</th>
                    <th>Provider</th>
                    <th>Usage</th>
                    <th>Unit</th>
                </tr>
            </thead>
            <tbody>`;

        history.forEach(item => {
            const date = new Date(item.timestamp).toLocaleString();
            html += `
                <tr>
                    <td>${date}</td>
                    <td>${item.provider_name}</td>
                    <td>${item.usage.toFixed(2)}</td>
                    <td>${item.usage_unit}</td>
                </tr>
            `;
        });

        html += '</tbody></table>';
        container.innerHTML = html;
    } catch (e) {
        console.error('Failed to load history:', e);
        container.innerHTML = `<div class="error">Failed to load history: ${e}</div>`;
    }
}

/**
 * Load agent history
 */
async function loadAgentHistoryAsync() {
    const container = document.getElementById('agent-list');
    container.innerHTML = '<div class="loading">Loading agent history...</div>';

    try {
        const now = new Date();
        const thirtyDaysAgo = new Date(now.setDate(now.getDate() - 30));
        const startDate = thirtyDaysAgo.toISOString();

        const history = await window.AicUtils.invoke('get_historical_usage_from_agent', {
            limit: 100,
            startDate: startDate
        });

        if (!history || history.length === 0) {
            container.innerHTML = '<div class="loading">No agent history found. (Retention: 30 days)</div>';
            return;
        }

        let html = `<table class="history-table">
            <thead>
                <tr>
                    <th>Time</th>
                    <th>Provider</th>
                    <th>Usage</th>
                    <th>Limit</th>
                    <th>Reset</th>
                </tr>
            </thead>
            <tbody>`;

        history.forEach(item => {
            const date = new Date(item.timestamp * 1000).toLocaleString();
            html += `
                <tr>
                    <td style="white-space: nowrap;">${date}</td>
                    <td style="white-space: nowrap;">${item.provider_id}</td>
                    <td>${item.usage !== null ? item.usage : '-'}</td>
                    <td>${item.limit !== null ? item.limit : '-'}</td>
                    <td>${item.next_reset ? new Date(item.next_reset * 1000).toLocaleDateString() : '-'}</td>
                </tr>
            `;
        });

        html += '</tbody></table>';
        container.innerHTML = html;
    } catch (e) {
        console.error('Failed to load agent history:', e);
        container.innerHTML = `<div class="error">Failed to load agent history: ${e}</div>`;
    }
}

/**
 * Load raw logs
 */
async function loadRawLogsAsync() {
    const container = document.getElementById('logs-list');
    container.innerHTML = '<div class="loading">Loading debug logs...</div>';

    try {
        const logs = await window.AicUtils.invoke('get_raw_responses_from_agent', { limit: 20 });
        if (!logs || logs.length === 0) {
            container.innerHTML = '<div class="loading">No raw logs found. (Retention: 24h)</div>';
            return;
        }

        let html = `<table class="history-table">
            <thead>
                <tr>
                    <th>Time</th>
                    <th>Provider</th>
                    <th>Response Body</th>
                </tr>
            </thead>
            <tbody>`;

        logs.forEach(log => {
            const date = new Date(log.timestamp * 1000).toLocaleString();
            html += `
                <tr>
                    <td style="white-space: nowrap;">${date}</td>
                    <td style="white-space: nowrap;">${log.provider_id}</td>
                    <td><div class="log-body">${window.AicUtils.escapeHtml(log.response_body)}</div></td>
                </tr>
            `;
        });

        html += '</tbody></table>';
        container.innerHTML = html;
    } catch (e) {
        console.error('Failed to load logs:', e);
        container.innerHTML = `<div class="error">Failed to load logs: ${e}</div>`;
    }
}

/**
 * Load settings
 */
async function loadSettings() {
    console.log('=== loadSettings() START ===');
    console.log('Timestamp:', new Date().toISOString());

    setupAgentStatusListener();
    loadProvidersDataAsync();
    loadCachedUsageData();
    await updateCachedBadge();

    await new Promise(resolve => {
        const check = () => {
            if (window.preloadedAgentInfo !== undefined) {
                resolve();
            } else {
                setTimeout(check, 50);
            }
        };
        check();
    });

    loadAgentStatusAsync();
    await updateCachedBadge();

    console.log('=== loadSettings() END (async calls started) ===');
}

/**
 * Load cached usage data
 */
function loadCachedUsageData() {
    const cached = window.AicUtils?.loadCachedUsageData();
    if (cached) {
        usageData = cached;
        console.log('[CACHE] Settings loaded usage data from localStorage:', usageData.length, 'providers');
    }
}

/**
 * Load providers data async
 */
async function loadProvidersDataAsync() {
    console.log('=== loadProvidersDataAsync() START ===');
    let dataLoaded = false;

    if (window.AicUtils?.invoke) {
        try {
            const preloaded = await window.AicUtils.invoke('get_preloaded_settings_data');
            if (preloaded) {
                configs = preloaded[0] || [];
                usageData = preloaded[1] || [];
                if (preloaded[2]) {
                    window.preloadedAgentInfo = preloaded[2];
                }
                dataLoaded = true;
                await updateCachedBadge();
            }
        } catch (preloadErr) {
            console.log('Could not get preloaded data:', preloadErr.message || preloadErr);
        }
    }

    if (!dataLoaded && window.AicUtils?.invoke) {
        console.log('Loading providers from agent...');
        try {
            const uiData = await window.AicUtils.invoke('get_all_ui_data');
            configs = uiData?.providers || [];
            usageData = uiData?.usage || [];
            githubAuthStatus = uiData?.github_auth || { is_authenticated: false, username: '', token_invalid: false };

            if (window.AicUtils?.invoke) {
                await window.AicUtils.invoke('set_data_live', { isLive: true });
            }
            freshDataArrived = true;
            await updateCachedBadge();

            if (uiData?.agent_info) {
                window.preloadedAgentInfo = uiData.agent_info;
                updateAgentInfoDisplay(uiData.agent_info);
            }
        } catch (agentErr) {
            console.error('ERROR calling get_all_ui_data:', agentErr);
            configs = [];
            usageData = [];
        }
    }

    console.log('Calling populateProviders() with', configs.length, 'configs');
    populateProviders();
    console.log('=== loadProvidersDataAsync() END ===');
}

/**
 * Update agent info display
 */
async function updateAgentInfoDisplay(agentInfo) {
    const agentVersionEl = document.getElementById('agentVersion');
    const agentPathEl = document.getElementById('agentPath');
    const agentPortEl = document.getElementById('agentPort');
    const agentUrlEl = document.getElementById('agentUrl');

    if (agentVersionEl) agentVersionEl.textContent = agentInfo.version || 'Unknown';
    if (agentPathEl) agentPathEl.textContent = agentInfo.agent_path || 'Unknown';

    const port = await window.AicUtils?.getAgentPort() || 8080;
    if (agentPortEl) agentPortEl.textContent = port;
    if (agentUrlEl) agentUrlEl.textContent = `http://localhost:${port}`;
}

/**
 * Load preferences
 */
async function loadPreferencesAsync() {
    console.log('=== loadPreferencesAsync() START ===');
    try {
        if (window.AicUtils?.invoke) {
            prefs = await window.AicUtils.invoke('load_preferences') || {};
        }
        populateLayout();
        populateFonts();
    } catch (e) {
        console.error('CRITICAL ERROR in loadPreferencesAsync:', e);
    }
}

/**
 * Load agent status
 */
async function loadAgentStatusAsync() {
    console.log('=== loadAgentStatusAsync() START ===');
    await checkAgentStatus();

    if (window.preloadedAgentInfo) {
        console.log('Using preloaded agent info:', window.preloadedAgentInfo);
        const agentVersionEl = document.getElementById('agentVersion');
        const agentPathEl = document.getElementById('agentPath');
        if (agentVersionEl) agentVersionEl.textContent = window.preloadedAgentInfo.version || 'Unknown';
        if (agentPathEl) agentPathEl.textContent = window.preloadedAgentInfo.agent_path || 'Unknown';
        console.log('=== loadAgentStatusAsync() END (preloaded) ===');
        return;
    }

    const agentVersionEl = document.getElementById('agentVersion');
    const agentPathEl = document.getElementById('agentPath');

    if (agentVersionEl && agentPathEl) {
        try {
            const port = await window.AicUtils?.getAgentPort() || 8080;
            const response = await fetch(`http://localhost:${port}/api/agent/info`);
            if (response.ok) {
                const info = await response.json();
                agentVersionEl.textContent = info.version || 'Unknown';
                agentPathEl.textContent = info.agent_path || 'Unknown';
            } else {
                agentVersionEl.textContent = 'Agent not running';
                agentPathEl.textContent = 'Agent not running';
            }
        } catch (e) {
            agentVersionEl.textContent = 'Agent not running';
            agentPathEl.textContent = 'Agent not running';
        }
    }
    console.log('=== loadAgentStatusAsync() END ===');
}

/**
 * Load app version
 */
async function loadAppVersion() {
    try {
        const version = await window.AicUtils?.invoke('get_app_version');
        const currentVersionEl = document.getElementById('currentVersion');
        if (currentVersionEl && version) {
            currentVersionEl.textContent = 'v' + version;
        }
    } catch (e) {
        console.log('Could not load app version:', e);
    }
}

/**
 * Setup agent status listener
 */
function setupAgentStatusListener() {
    if (window.__TAURI__?.event) {
        window.__TAURI__.event.listen('agent-status-changed', async (event) => {
            const status = event.payload;
            console.log('Settings received agent status:', status);
            updateAgentStatusUI(status);
        });
    }
}

/**
 * Update agent status UI
 */
function updateAgentStatusUI(status) {
    const statusText = document.getElementById('agentStatusText');
    const startBtn = document.getElementById('startAgentBtn');
    const stopBtn = document.getElementById('stopAgentBtn');

    if (status.status === 'connected') {
        if (statusText) {
            statusText.textContent = 'Running';
            statusText.style.color = 'var(--accent-green)';
        }
        if (startBtn) startBtn.style.display = 'none';
        if (stopBtn) stopBtn.style.display = 'inline-block';
    } else if (status.status === 'connecting') {
        if (statusText) {
            statusText.textContent = 'Starting...';
            statusText.style.color = '#FFD700';
        }
        if (startBtn) startBtn.style.display = 'none';
        if (stopBtn) stopBtn.style.display = 'inline-block';
    } else {
        if (statusText) {
            statusText.textContent = 'Stopped';
            statusText.style.color = 'var(--text-secondary)';
        }
        if (startBtn) startBtn.style.display = 'inline-block';
        if (stopBtn) stopBtn.style.display = 'none';
    }

    const agentPortEl = document.getElementById('agentPort');
    const agentUrlEl = document.getElementById('agentUrl');
    if (agentPortEl) agentPortEl.textContent = status.port || 8080;
    if (agentUrlEl) agentUrlEl.textContent = `http://localhost:${status.port || 8080}`;
}

/**
 * Check agent status
 */
async function checkAgentStatus() {
    try {
        const status = await window.AicUtils?.invoke('get_agent_status');
        console.log('Agent status:', status);
        updateAgentStatusUI(status);

        if (status.status === 'connected' && !window.preloadedAgentInfo) {
            const port = await window.AicUtils?.getAgentPort() || 8080;
            try {
                const response = await fetch(`http://localhost:${port}/api/agent/info`);
                if (response.ok) {
                    const info = await response.json();
                    const agentPathEl = document.getElementById('agentPath');
                    const agentVersionEl = document.getElementById('agentVersion');
                    if (agentPathEl) agentPathEl.textContent = info.agent_path || 'Unknown';
                    if (agentVersionEl) agentVersionEl.textContent = info.version || 'Unknown';
                }
            } catch (e) {
                console.error('Failed to fetch agent info:', e);
            }
        }
    } catch (e) {
        console.error('Failed to check agent status:', e);
        const statusText = document.getElementById('agentStatusText');
        if (statusText) {
            statusText.textContent = 'Error';
            statusText.style.color = 'var(--accent-red)';
        }
    }
}

/**
 * Start agent
 */
async function startAgent() {
    console.log('Starting agent from settings...');
    try {
        const success = await window.AicUtils?.invoke('start_agent');
        if (success) {
            console.log('Agent started successfully');
            await checkAgentStatus();
            setTimeout(() => loadSettings(), 2000);
        }
    } catch (e) {
        console.error('Failed to start agent:', e);
        alert('Failed to start agent: ' + (e?.message || e));
    }
}

/**
 * Stop agent
 */
async function stopAgent() {
    console.log('Stopping agent from settings...');
    try {
        const success = await window.AicUtils?.invoke('stop_agent');
        if (success) {
            console.log('Agent stopped successfully');
            await checkAgentStatus();
        }
    } catch (e) {
        console.error('Failed to stop agent:', e);
        alert('Failed to stop agent: ' + (e?.message || e));
    }
}

/**
 * Discover tokens
 */
async function discoverTokens() {
    console.log('Discovering tokens via agent...');
    const statusDiv = document.getElementById('discoveryStatus');
    const btn = document.getElementById('discoverTokenBtn');

    if (statusDiv) statusDiv.textContent = 'Discovering tokens...';
    if (btn) btn.disabled = true;

    try {
        const port = await window.AicUtils?.getAgentPort() || 8080;
        const response = await fetch(`http://localhost:${port}/api/discover`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        });

        if (response.ok) {
            const result = await response.json();
            console.log('Token discovery result:', result);

            if (result.discovered && result.discovered.length > 0) {
                if (statusDiv) statusDiv.textContent = `Discovered ${result.discovered.length} token(s). Reloading providers...`;
                setTimeout(() => loadSettings(), 1000);
            } else {
                if (statusDiv) statusDiv.textContent = 'No new tokens found.';
            }
        } else {
            const errorText = await response.text();
            console.error('Discovery failed:', errorText);
            if (statusDiv) statusDiv.textContent = 'Discovery failed. Is the agent running?';
        }
    } catch (e) {
        console.error('Failed to discover tokens:', e);
        if (statusDiv) statusDiv.textContent = 'Error: ' + (e?.message || 'Failed to connect to agent');
    } finally {
        if (btn) btn.disabled = false;
    }
}

/**
 * Populate providers list
 */
function populateProviders() {
    console.log('=== populateProviders() START ===');
    const container = document.getElementById('providers-list');

    if (!container) {
        console.error('ERROR: providers-list container not found!');
        return;
    }

    if (!configs || configs.length === 0) {
        console.warn('WARNING: No configs to display');
        container.innerHTML = '<div class="error">No providers found. Make sure the agent is running.</div>';
        return;
    }

    configs.sort((a, b) => {
        const infoA = window.AicUtils?.getProviderInfo(a.provider_id) || { name: a.provider_id };
        const infoB = window.AicUtils?.getProviderInfo(b.provider_id) || { name: b.provider_id };
        return infoA.name.localeCompare(infoB.name);
    });

    const html = configs.map((config) => {
        const info = window.AicUtils?.getProviderInfo(config.provider_id) || { name: config.provider_id, icon: config.provider_id.charAt(0).toUpperCase(), color: '#007ACC' };
        const hasKey = config.api_key && config.api_key.length > 0;
        
        const antigravityUsage = usageData.find(u => u.provider_id === 'antigravity');
        const isAntigravityConnected = config.provider_id === 'antigravity' && antigravityUsage && antigravityUsage.is_available;

        let displayName = info.name;
        let accountName = '';
        
        if (config.provider_id === 'github-copilot') {
            accountName = githubAuthStatus.username || '';
            if (accountName) {
                const maskedName = window.privacyMode ? '***' : accountName;
                displayName = info.name + ' [' + maskedName + ']';
            }
        } else {
            const usage = usageData.find(u => u.provider_id === config.provider_id);
            accountName = usage?.account_name || config.account_name || '';
            if (accountName && config.provider_id !== 'github-copilot') {
                const maskedName = window.privacyMode ? '***' : accountName;
                displayName = info.name + ' [' + maskedName + ']';
            }
        }

        let authSourceDisplay = config.auth_source || 'None';
        if (authSourceDisplay === 'Environment Variable') authSourceDisplay = 'Env';
        else if (authSourceDisplay === 'Environment') authSourceDisplay = 'Env';
        else if (authSourceDisplay === 'AI Consumption Tracker') authSourceDisplay = 'AICT';

        let subModelsHtml = '';
        if (config.provider_id === 'antigravity') {
            const usage = usageData.find(u => u.provider_id === 'antigravity');
            if (usage && usage.details && Array.isArray(usage.details) && usage.details.length > 0) {
                subModelsHtml = `
                    <div style="margin-top: 10px; padding-top: 10px; border-top: 1px solid #3a3a3a;">
                        <div style="font-size: 11px; color: #888; font-weight: 600; margin-bottom: 8px;">Individual Quota Icons:</div>
                        ${usage.details.map(detail => `
                            <label class="checkbox-label" style="display: block; margin: 4px 0; margin-left: 10px; font-size: 11px;">
                                <input type="checkbox" class="subtray-checkbox" data-provider="${config.provider_id}" data-detail="${detail.name}" ${config.enabled_sub_trays && config.enabled_sub_trays.includes(detail.name) ? 'checked' : ''}>
                                <span style="color: #ccc;">${detail.name}</span>
                            </label>
                        `).join('')}
                    </div>
                `;
            }
        }

        return `
            <div class="provider-card" data-provider="${config.provider_id}">
                <div class="provider-header">
                    <div class="provider-info">
                        <div class="provider-icon" style="background: ${info.color};">${info.icon}</div>
                        <span class="provider-name">${displayName}</span>
                    </div>
                    <div class="provider-actions">
                        <span class="auth-source-label" title="Configuration Source: ${config.auth_source || 'None'}">${authSourceDisplay}</span>
                        <label class="checkbox-label">
                            <input type="checkbox" class="tray-checkbox" ${config.show_in_tray ? 'checked' : ''}>
                            <span>Tray</span>
                        </label>
                        <span class="status-badge ${hasKey || isAntigravityConnected ? 'active' : 'inactive'}">${hasKey ? 'Active' : (isAntigravityConnected ? 'Connected' : 'Inactive')}</span>
                    </div>
                </div>
                ${renderProviderActions(config)}
                ${subModelsHtml}
            </div>
        `;
    }).join('');

    container.innerHTML = html;
    applyPrivacyMode();

    document.querySelectorAll('.tray-checkbox').forEach(el => {
        el.addEventListener('change', () => settingsChanged = true);
    });

    document.querySelectorAll('.api-key-input').forEach(el => {
        el.addEventListener('input', () => settingsChanged = true);
    });

    setupAuthButtons();
    console.log('=== populateProviders() END ===');
}

/**
 * Render provider-specific actions
 */
function renderProviderActions(config) {
    if (config.provider_id === 'github-copilot') {
        const usage = usageData.find(u => u.provider_id === 'github-copilot');
        const username = githubAuthStatus.username || usage?.account_name || '';
        const isAuthenticated = githubAuthStatus.is_authenticated;
        const tokenInvalid = githubAuthStatus.token_invalid;
        const displayUsername = (window.privacyMode && username) ? '***' : username;
        
        let authStatus, authColor;
        if (tokenInvalid) {
            authStatus = 'Token Invalid - Please Re-authenticate';
            authColor = '#FF6B6B';
        } else {
            authStatus = isAuthenticated
                ? (username ? `Authenticated (${displayUsername})` : 'Authenticated')
                : 'Not Authenticated';
            authColor = isAuthenticated ? '#90EE90' : '#888';
        }
        
        const btnText = isAuthenticated ? 'Log out' : 'Log in';
        
        return `
            <div class="provider-row" style="display: flex; align-items: center; gap: 10px;">
                <span style="color: ${authColor}; font-size: 11px;">${authStatus}</span>
                <button class="auth-btn" data-provider="github-copilot"
                    style="padding: 4px 12px; font-size: 11px; background: #333; color: #fff; border: 1px solid #555; border-radius: 3px; cursor: pointer;">
                    ${btnText}
                </button>
            </div>
        `;
    } else if (config.provider_id === 'antigravity') {
        const antigravityUsage = usageData.find(u => u.provider_id === 'antigravity');
        const isAntigravityConnected = antigravityUsage && antigravityUsage.is_available;
        return `
            <div class="provider-row">
                <span style="color: ${isAntigravityConnected ? '#90EE90' : '#888'}; font-size: 11px;">
                    ${isAntigravityConnected ? 'Running (Connected)' : 'Not Running'}
                </span>
            </div>
        `;
    } else {
        return `
            <div class="provider-row">
                <input type="text" class="api-key-input" id="api-key-${config.provider_id}"
                    value="${config.api_key || ''}" 
                    placeholder="Enter API key"
                    data-provider="${config.provider_id}">
            </div>
        `;
    }
}

/**
 * Setup auth button listeners
 */
function setupAuthButtons() {
    document.querySelectorAll('.auth-btn').forEach(el => {
        el.addEventListener('click', async (e) => {
            const providerId = e.target.dataset.provider;
            if (providerId === 'github-copilot') {
                const config = configs.find(c => c.provider_id === 'github-copilot');
                const isAuthenticated = config && config.api_key && config.api_key.length > 0;

                if (isAuthenticated) {
                    config.api_key = '';
                    config.account_name = '';
                    settingsChanged = true;
                    populateProviders();
                } else {
                    await initiateGitHubLogin();
                }
            }
        });
    });
}

/**
 * Initiate GitHub login
 */
async function initiateGitHubLogin() {
    try {
        const port = await window.AicUtils?.getAgentPort() || 8080;
        const response = await fetch(`http://localhost:${port}/api/auth/github/device`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        });
        const data = await response.json();

        if (data.success) {
            const code = data.user_code;
            const verificationUrl = data.verification_uri;

            window.open(verificationUrl, '_blank');

            document.getElementById('authContent').innerHTML = `
                <p>Please enter this code on the GitHub page:</p>
                <div style="display: flex; align-items: center; gap: 10px; margin: 15px 0;">
                    <code style="font-size: 24px; font-weight: bold; padding: 10px; background: #f0f0f0; border-radius: 4px; flex: 1; text-align: center;">${code}</code>
                    <button class="btn btn-secondary" onclick="navigator.clipboard.writeText('${code}').then(() => this.textContent='Copied!')">Copy</button>
                </div>
                <p style="font-size: 12px; color: #666;">If the page didn't open, <a href="${verificationUrl}" target="_blank">click here</a></p>
                <p id="authStatus" style="margin-top: 15px;">Waiting for authorization...</p>
            `;
            document.getElementById('authModal').classList.add('visible');
            pollForGitHubToken(data.device_code, data.interval);
        }
    } catch (err) {
        console.error('GitHub auth initiation failed:', err);
        alert('Failed to initiate GitHub login: ' + err.message);
    }
}

/**
 * Poll for GitHub token
 */
async function pollForGitHubToken(deviceCode, interval) {
    const maxAttempts = 60;
    let attempts = 0;

    const pollInterval = setInterval(async () => {
        attempts++;
        if (attempts > maxAttempts) {
            clearInterval(pollInterval);
            document.getElementById('authStatus').innerHTML = '<span style="color: red;">Login timed out. Please try again.</span>';
            return;
        }

        document.getElementById('authStatus').textContent = `Waiting for authorization... (attempt ${attempts}/${maxAttempts})`;

        try {
            const port = await window.AicUtils?.getAgentPort() || 8080;
            const response = await fetch(`http://localhost:${port}/api/auth/github/poll`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ device_code: deviceCode, interval: interval })
            });
            const data = await response.json();

            if (data.success) {
                clearInterval(pollInterval);
                document.getElementById('authStatus').innerHTML = '<span style="color: green;">Authentication successful!</span>';

                const config = configs.find(c => c.provider_id === 'github-copilot');
                if (config) {
                    config.api_key = data.token;
                    config.auth_source = 'GitHub OAuth';
                    settingsChanged = true;
                    populateProviders();

                    try {
                        const port = await window.AicUtils?.getAgentPort() || 8080;
                        const userResponse = await fetch(`http://localhost:${port}/api/auth/github/status`);
                        const userData = await userResponse.json();
                        if (userData.username) {
                            config.account_name = userData.username;
                            populateProviders();
                        }
                    } catch (err) {
                        console.error('Failed to fetch username:', err);
                    }

                    if (window.AicUtils?.invoke) {
                        await window.AicUtils.invoke('save_provider_configs', { configs });
                    }
                }

                setTimeout(() => closeAuthModal(), 1500);
            } else if (data.status === 'expired' || data.status === 'access_denied') {
                clearInterval(pollInterval);
                document.getElementById('authStatus').innerHTML = '<span style="color: red;">Login failed: ' + (data.error || 'Unknown error') + '</span>';
            }
        } catch (err) {
            console.error('GitHub token poll error:', err);
        }
    }, interval * 1000);
}

/**
 * Populate layout settings
 */
function populateLayout() {
    document.getElementById('compactMode').checked = prefs.compact_mode || false;
    document.getElementById('alwaysOnTop').checked = prefs.always_on_top || false;
    document.getElementById('stayOpen').checked = prefs.stay_open || false;
    document.getElementById('showAll').checked = prefs.show_all !== false;
    document.getElementById('refreshInterval').value = prefs.refresh_interval || 300;
    document.getElementById('yellowThreshold').value = prefs.yellow_threshold || 60;
    document.getElementById('redThreshold').value = prefs.red_threshold || 80;
    document.getElementById('invertProgressBar').checked = prefs.invert_progress_bar || false;
    document.getElementById('autoUpdate').checked = prefs.auto_update !== false;
}

/**
 * Populate font settings
 */
function populateFonts() {
    document.getElementById('fontFamily').value = prefs.font_family || '-apple-system';
    document.getElementById('fontSize').value = prefs.font_size || 13;
    document.getElementById('fontBold').checked = prefs.font_bold || false;
    document.getElementById('fontItalic').checked = prefs.font_italic || false;
}

/**
 * Update font preview
 */
function updateFontPreview() {
    const preview = document.getElementById('fontPreviewText');
    const family = document.getElementById('fontFamily').value;
    const size = document.getElementById('fontSize').value;
    const bold = document.getElementById('fontBold').checked;
    const italic = document.getElementById('fontItalic').checked;

    preview.style.fontFamily = family;
    preview.style.fontSize = size + 'px';
    preview.style.fontWeight = bold ? 'bold' : 'normal';
    preview.style.fontStyle = italic ? 'italic' : 'normal';
}

/**
 * Reset font settings
 */
function resetFontSettings() {
    document.getElementById('fontFamily').value = '-apple-system';
    document.getElementById('fontSize').value = '13';
    document.getElementById('fontBold').checked = false;
    document.getElementById('fontItalic').checked = false;
    updateFontPreview();
    settingsChanged = true;
}

/**
 * Update cached badge
 */
async function updateCachedBadge() {
    const cachedBadge = document.getElementById('cachedBadge');
    if (!cachedBadge) return;

    try {
        const status = await window.AicUtils?.invoke('get_data_status');
        console.log('[CACHE] Settings badge - Rust says:', status, 'usageData length:', usageData?.length);

        if (usageData && usageData.length > 0) {
            const label = (status.label === 'Live' && !freshDataArrived) ? 'Cached' : status.label;
            const css_class = (status.label === 'Live' && !freshDataArrived) ? 'cached' : (status.css_class || '');

            cachedBadge.textContent = label;
            cachedBadge.className = 'cached-badge ' + css_class;
            cachedBadge.style.display = 'inline-block';
        } else {
            cachedBadge.style.display = 'none';
        }
    } catch (e) {
        console.log('[CACHE] Settings could not query data status:', e);
        if (usageData && usageData.length > 0) {
            cachedBadge.textContent = 'Cached';
            cachedBadge.className = 'cached-badge';
            cachedBadge.style.display = 'inline-block';
        } else {
            cachedBadge.style.display = 'none';
        }
    }
}

/**
 * Apply privacy mode to inputs
 */
function applyPrivacyMode() {
    console.log('applyPrivacyMode called, privacyMode:', window.privacyMode);
    const apiKeyInputs = document.querySelectorAll('.api-key-input');
    console.log('Found', apiKeyInputs.length, 'API key inputs');
    apiKeyInputs.forEach((input) => {
        try {
            if (window.privacyMode) {
                input.type = 'password';
            } else {
                input.type = 'text';
            }
        } catch (err) {
            console.error('ERROR changing input type:', err);
        }
    });
}

/**
 * Save settings
 */
async function saveSettings() {
    try {
        document.querySelectorAll('.provider-card').forEach(card => {
            const providerId = card.dataset.provider;
            const config = configs.find(c => c.provider_id === providerId);
            if (config) {
                const trayCheckbox = card.querySelector('.tray-checkbox');
                if (trayCheckbox) config.show_in_tray = trayCheckbox.checked;

                const apiKeyInput = card.querySelector(`#api-key-${providerId}`);
                if (apiKeyInput) config.api_key = apiKeyInput.value;

                if (providerId === 'antigravity') {
                    const subTrayCheckboxes = card.querySelectorAll('.subtray-checkbox');
                    config.enabled_sub_trays = [];
                    subTrayCheckboxes.forEach(cb => {
                        if (cb.checked) {
                            config.enabled_sub_trays.push(cb.dataset.detail);
                        }
                    });
                }
            }
        });

        prefs.compact_mode = document.getElementById('compactMode').checked;
        prefs.always_on_top = document.getElementById('alwaysOnTop').checked;
        prefs.stay_open = document.getElementById('stayOpen').checked;
        prefs.show_all = document.getElementById('showAll').checked;
        prefs.refresh_interval = parseInt(document.getElementById('refreshInterval').value) || 300;
        prefs.yellow_threshold = parseInt(document.getElementById('yellowThreshold').value) || 60;
        prefs.red_threshold = parseInt(document.getElementById('redThreshold').value) || 80;
        prefs.invert_progress_bar = document.getElementById('invertProgressBar').checked;
        prefs.auto_update = document.getElementById('autoUpdate').checked;
        prefs.font_family = document.getElementById('fontFamily').value;
        prefs.font_size = parseInt(document.getElementById('fontSize').value) || 13;
        prefs.font_bold = document.getElementById('fontBold').checked;
        prefs.font_italic = document.getElementById('fontItalic').checked;

        if (window.AicUtils?.invoke) {
            await window.AicUtils.invoke('save_provider_configs', { configs });
            await window.AicUtils.invoke('save_preferences', { preferences: prefs });
            await window.AicUtils.invoke('toggle_always_on_top', { enabled: prefs.always_on_top });

            try {
                await window.__TAURI__.event.emit('font-settings-changed', {
                    font_family: prefs.font_family,
                    font_size: prefs.font_size,
                    font_bold: prefs.font_bold,
                    font_italic: prefs.font_italic
                });
            } catch (e) {
                console.warn('Could not emit font settings event:', e);
            }
        }

        settingsChanged = true;
        closeSettings();
    } catch (e) {
        console.error('Error saving:', e);
        alert('Failed to save settings: ' + e.message);
    }
}

/**
 * Check for updates
 */
async function checkForUpdates() {
    const btn = document.getElementById('checkUpdatesBtn');
    const message = document.getElementById('updateMessage');

    btn.textContent = 'Checking...';
    btn.disabled = true;

    try {
        if (window.AicUtils?.invoke) {
            const result = await window.AicUtils.invoke('check_for_updates');
            if (result.update_available) {
                message.textContent = `Update available: ${result.latest_version}`;
                message.style.color = 'var(--accent-green)';
            } else {
                message.textContent = 'You are running the latest version.';
                message.style.color = 'var(--text-secondary)';
            }
        }
    } catch (e) {
        message.textContent = 'Failed to check for updates.';
        message.style.color = 'var(--accent-red)';
    } finally {
        btn.textContent = 'Check for Updates';
        btn.disabled = false;
    }
}

/**
 * Close auth modal
 */
function closeAuthModal() {
    document.getElementById('authModal').classList.remove('visible');
}

/**
 * Close settings window
 */
function closeSettings() {
    if (window.AicUtils?.invoke) {
        window.AicUtils.invoke('close_settings_window');
    }
}

// Privacy mode change handler for this window
function onPrivacyModeChanged(enabled) {
    window.privacyMode = enabled;
    applyPrivacyMode();
    populateProviders();
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', initSettingsWindow);

// Expose functions globally for inline event handlers
window.switchHistorySubTab = switchHistorySubTab;
window.refreshHistory = refreshHistory;
window.togglePrivacyMode = togglePrivacyMode;
window.resetFontSettings = resetFontSettings;
window.checkForUpdates = checkForUpdates;
window.closeAuthModal = closeAuthModal;
window.saveSettings = saveSettings;
window.startAgent = startAgent;
window.stopAgent = stopAgent;
window.discoverTokens = discoverTokens;
window.onPrivacyModeChanged = onPrivacyModeChanged;
