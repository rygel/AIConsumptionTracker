/*
 * AI Consumption Tracker - Main Window JavaScript (index.html)
 */

// Window-specific state
let showAll = true;
let privacyMode = false;
let usageData = [];
let agentConnected = false;
let githubAuthStatus = { is_authenticated: false, username: '', token_invalid: false };
let prefs = {
    yellow_threshold: 60,
    red_threshold: 80,
    invert_progress_bar: false
};

let isShowingCachedData = false;
let freshDataArrived = false;
let hadCachedDataInitially = false;
let statusCheckInterval = null;
let pollingAttempts = 0;

/**
 * Initialize the main window when DOM is ready
 */
async function initMainWindow() {
    console.log('[TIMING] UI_START');
    console.log('DOM loaded');
    console.log('Tauri object:', typeof window.__TAURI__);
    console.log('Tauri keys:', window.__TAURI__ ? Object.keys(window.__TAURI__) : 'none');

    setupCloseButton();
    setupButtons();
    setupPrivacyModeListener();
    loadPrivacyMode();
    setupToggleListeners();
    loadPreferences();
    loadAppVersion();
    loadData();
    updateAgentUI('connecting', 'Connecting...');
    setupStreamingListeners();
    setupAgentReadyListener();
    startPeriodicStatusCheck();
    setupAgentStatusListener();
    setupDataStatusListener();
    setupFontSettingsListener();
    setupFocusListener();
}

/**
 * Setup close button with IPC
 */
function setupCloseButton() {
    document.getElementById('closeBtn').addEventListener('click', function () {
        console.log('Close clicked');
        if (invoke) {
            invoke('close_window')
                .then(() => console.log('Close invoked'))
                .catch(e => console.error('Close error:', e));
        } else {
            console.error('No invoke method found');
        }
    });
}

/**
 * Setup button event handlers
 */
function setupButtons() {
    document.getElementById('agentBtn').addEventListener('click', toggleAgent);
    document.getElementById('privacyToggleBtn').addEventListener('click', togglePrivacy);
    document.getElementById('refreshBtn').addEventListener('click', refreshData);
    document.getElementById('settingsBtn').addEventListener('click', openSettings);
}

/**
 * Setup privacy mode
 */
function setupPrivacyModeListener() {
    window.AicUtils.setupPrivacyModeListener();
}

/**
 * Load privacy mode from localStorage
 */
function loadPrivacyMode() {
    privacyMode = window.AicUtils.loadPrivacyMode();
    const btn = document.getElementById('privacyToggleBtn');
    if (btn) {
        btn.classList.toggle('active', privacyMode);
        btn.title = privacyMode ? 'Privacy Mode On (Click to Show)' : 'Privacy Mode Off (Click to Hide)';
    }
}

/**
 * Setup toggle change listeners
 */
function setupToggleListeners() {
    document.getElementById('showAllToggle').addEventListener('change', refreshData);
    document.getElementById('alwaysOnTopToggle').addEventListener('change', toggleAlwaysOnTop);
}

/**
 * Load preferences from Tauri
 */
async function loadPreferences() {
    try {
        if (invoke) {
            const loadedPrefs = await invoke('load_preferences');
            if (loadedPrefs) {
                prefs = { ...prefs, ...loadedPrefs };
                console.log('Loaded preferences from Tauri:', prefs);
            }
        }

        const localAlwaysOnTop = localStorage.getItem('always_on_top');
        if (localAlwaysOnTop !== null) {
            prefs.always_on_top = JSON.parse(localAlwaysOnTop);
        }

        if (prefs.always_on_top) {
            const toggle = document.getElementById('alwaysOnTopToggle');
            if (toggle) {
                toggle.checked = true;
                if (invoke) {
                    await invoke('toggle_always_on_top', { enabled: true });
                }
            }
        }

        applyFontSettings();
    } catch (e) {
        console.log('Could not load preferences, using defaults:', e);
    }
}

/**
 * Load and display app version
 */
async function loadAppVersion() {
    try {
        console.log('Loading app version...');
        if (invoke) {
            const version = await invoke('get_app_version');
            console.log('Got version:', version);
            const versionElement = document.getElementById('versionText');
            if (versionElement && version) {
                versionElement.textContent = 'v' + version;
            }
        }
    } catch (e) {
        console.error('Could not load app version:', e);
    }
}

/**
 * Show skeleton placeholders while loading
 */
function showSkeletonPlaceholders() {
    const container = document.getElementById('providersList');
    container.innerHTML = `
        <div class="skeleton-group">
            <div class="skeleton-header"></div>
            <div class="skeleton-item"></div>
            <div class="skeleton-item"></div>
        </div>
        <div class="skeleton-group">
            <div class="skeleton-header"></div>
            <div class="skeleton-item"></div>
        </div>
    `;
}

/**
 * Load initial data
 */
async function loadData() {
    console.log('[TIMING] LOAD_DATA_CALLED: ' + (Date.now() - window.appStartTime) + 'ms');
    const container = document.getElementById('providersList');

    const cachedData = window.AicUtils.loadCachedUsageData();
    if (cachedData && cachedData.length > 0) {
        console.log('[CACHE] Showing cached data immediately');
        usageData = cachedData;
        isShowingCachedData = true;
        hadCachedDataInitially = true;
        renderProviders();
    } else {
        hadCachedDataInitially = false;
        showSkeletonPlaceholders();
    }

    requestStreamingData();
    loadGitHubAuthAsync();

    if (!hadCachedDataInitially) {
        setTimeout(() => {
            if (!freshDataArrived) {
                console.log('[CACHE] Timeout with no cache - showing empty state');
                const container = document.getElementById('providersList');
                container.innerHTML = '<div class="loading">Connecting...</div>';
            }
        }, 15000);
    }
}

/**
 * Fetch GitHub authentication status
 */
async function fetchGitHubAuthStatus() {
    try {
        const port = await window.AicUtils.getAgentPort();
        const response = await fetch(`http://localhost:${port}/api/auth/github/status`);
        if (response.ok) {
            githubAuthStatus = await response.json();
            console.log('GitHub auth status:', githubAuthStatus);
        }
    } catch (e) {
        console.error('Failed to fetch GitHub auth status:', e);
        githubAuthStatus = { is_authenticated: false, username: '', token_invalid: false };
    }
}

/**
 * Load GitHub auth asynchronously
 */
async function loadGitHubAuthAsync() {
    try {
        const port = await window.AicUtils.getAgentPort();
        const response = await fetch(`http://localhost:${port}/api/auth/github/status`);
        if (response.ok) {
            githubAuthStatus = await response.json();
            console.log('GitHub auth status:', githubAuthStatus);
            renderProviders();
        }
    } catch (e) {
        console.log('GitHub auth: Agent not ready yet');
        githubAuthStatus = { is_authenticated: false, username: '', token_invalid: false };
    }
}

/**
 * Apply font settings to the UI
 */
function applyFontSettings() {
    const fontFamily = prefs.font_family || '-apple-system, BlinkMacSystemFont, Segoe UI, sans-serif';
    const fontSize = prefs.font_size || 13;
    const fontBold = prefs.font_bold || false;
    const fontItalic = prefs.font_italic || false;

    document.body.style.fontFamily = fontFamily;
    document.body.style.fontSize = fontSize + 'px';
    document.body.style.fontWeight = fontBold ? 'bold' : 'normal';
    document.body.style.fontStyle = fontItalic ? 'italic' : 'normal';

    const applyToElements = (selector) => {
        document.querySelectorAll(selector).forEach(el => {
            el.style.fontFamily = fontFamily;
            el.style.fontSize = fontSize + 'px';
            el.style.fontWeight = fontBold ? 'bold' : 'normal';
            el.style.fontStyle = fontItalic ? 'italic' : 'normal';
        });
    };

    applyToElements('.provider-item');
    applyToElements('.provider-content');
    applyToElements('.provider-name');

    const statusSize = Math.max(8, fontSize - 2);
    document.querySelectorAll('.provider-status').forEach(el => {
        el.style.fontFamily = fontFamily;
        el.style.fontSize = statusSize + 'px';
        el.style.fontWeight = fontBold ? 'bold' : 'normal';
        el.style.fontStyle = fontItalic ? 'italic' : 'normal';
    });

    const progressSize = Math.max(8, fontSize - 3);
    document.querySelectorAll('.progress-text').forEach(el => {
        el.style.fontFamily = fontFamily;
        el.style.fontSize = progressSize + 'px';
    });

    console.log('Applied font settings:', { fontFamily, fontSize, fontBold, fontItalic });
}

/**
 * Refresh data from agent
 */
async function refreshData() {
    showAll = document.getElementById('showAllToggle').checked;
    document.getElementById('refreshBtn').style.opacity = '0.5';

    refreshUsageDataAsync();
    refreshGitHubAuthAsync();

    if (invoke) {
        invoke('preload_settings_data')
            .then(() => console.log('Settings data preloaded after refresh'))
            .catch(e => console.log('Could not preload settings data:', e));
    }

    setTimeout(() => {
        document.getElementById('refreshBtn').style.opacity = '1';
    }, 500);
}

/**
 * Refresh usage data from agent
 */
async function refreshUsageDataAsync() {
    try {
        let data = [];
        if (invoke) {
            data = await invoke('refresh_usage_from_agent');
            console.log('Refreshed usage from agent:', data);
        }

        if (data && data.length > 0) {
            freshDataArrived = true;
            isShowingCachedData = false;
            usageData = data;
            window.AicUtils.saveUsageDataToCache(usageData);
        }
        renderProviders();

        if (invoke) {
            invoke('set_data_live', { isLive: true })
                .catch(e => console.error('Failed to set data live:', e));
        }
    } catch (err) {
        console.error('Error refreshing usage data:', err);
    }
}

/**
 * Refresh GitHub auth
 */
async function refreshGitHubAuthAsync() {
    try {
        await fetchGitHubAuthStatus();
        renderProviders();
    } catch (err) {
        console.error('Error refreshing GitHub auth:', err);
    }
}

/**
 * Render all providers in the UI
 */
function renderProviders() {
    const renderStartTime = Date.now();
    console.log('[TIMING] RENDER_START: ' + (renderStartTime - window.appStartTime) + 'ms');
    const container = document.getElementById('providersList');
    let filtered = showAll ? usageData : usageData.filter(u => u.is_available);

    if (filtered.length === 0) {
        container.innerHTML = '<div class="empty-state"><div style="font-size: 24px; margin-bottom: 10px;">📊</div><div>No providers configured</div><div style="font-size: 11px; color: #666; margin-top: 8px;">Use CLI: aic-cli auth &lt;provider&gt;</div></div>';
        return;
    }

    const quotaItems = filtered.filter(u => u.is_quota_based || u.payment_type === 'credits');
    const payGoItems = filtered.filter(u => !u.is_quota_based && u.payment_type !== 'credits');

    const sortByName = (a, b) => a.provider_name.localeCompare(b.provider_name);
    quotaItems.sort(sortByName);
    payGoItems.sort(sortByName);

    let groupCollapseState = {
        'Plans & Quotas': localStorage.getItem('group_collapsed_plans') === 'true',
        'Pay As You Go': localStorage.getItem('group_collapsed_paygo') === 'true'
    };

    function getProviderCollapsed(providerId) {
        return localStorage.getItem(`provider_collapsed_${providerId}`) === 'true';
    }

    function setProviderCollapsed(providerId, collapsed) {
        localStorage.setItem(`provider_collapsed_${providerId}`, collapsed.toString());
    }

    const createGroupHeader = (title, color, groupId) => {
        const isCollapsed = groupCollapseState[title] || false;
        return `
            <div class="group-header" data-group="${groupId}" style="display: flex; align-items: center; margin: 16px 0 10px 0; cursor: pointer; user-select: none;" title="Click to ${isCollapsed ? 'expand' : 'collapse'}">
                <span class="group-toggle" style="font-size: 10px; margin-right: 6px; transition: transform 0.2s;">${isCollapsed ? '▶' : '▼'}</span>
                <span style="font-size: 10px; font-weight: bold; color: ${color}; text-transform: uppercase; letter-spacing: 1px; margin-right: 10px;">${title}</span>
                <div style="flex: 1; height: 1px; background: ${color}; opacity: 0.3;"></div>
            </div>
            <div class="group-content" data-group="${groupId}" style="display: ${isCollapsed ? 'none' : 'block'};">
        `;
    };

    const renderProvider = (p) => {
        let providerHtml = '';
        let pct = p.usage_percentage || 0;
        const avail = p.is_available;

        let displayPct = pct;
        let barWidth = pct;
        if (p.remaining_percentage !== undefined && p.remaining_percentage !== null) {
            barWidth = p.remaining_percentage;
            displayPct = p.remaining_percentage;
        } else if (prefs.invert_progress_bar && avail) {
            displayPct = Math.max(0, 100 - pct);
            barWidth = displayPct;
        }

        let cls = 'low';
        if (!avail) cls = 'inactive';
        else if (p.usage_unit === 'Status') cls = 'status';
        else if (pct >= prefs.red_threshold) cls = 'high';
        else if (pct >= prefs.yellow_threshold) cls = 'medium';

        const type = p.is_quota_based ? 'Quota' : p.payment_type === 'credits' ? 'Credits' : 'Pay-As-You-Go';

        let desc = '';
        if (!avail) {
            desc = p.description || 'Not Available';
        } else if (p.usage_unit === 'Status') {
            desc = p.description || 'Connected';
        } else if (p.payment_type === 'credits' && p.cost_limit > 0) {
            const remaining = p.cost_limit - p.cost_used;
            desc = `${remaining.toFixed(2)} ${p.usage_unit || 'Credits'} Remaining`;
        } else if (p.payment_type === 'usage_based' && p.cost_limit > 0) {
            desc = `Spent: ${p.cost_used.toFixed(2)} ${p.usage_unit || 'USD'} / Limit: ${p.cost_limit.toFixed(2)}`;
        } else {
            desc = p.description || `${displayPct.toFixed(0)}%`;
        }

        if (privacyMode && p.cost_used > 0) desc = '••••••••';

        const width = avail ? Math.min(barWidth, 100) : 0;

        let progressClass = 'low';
        if (!avail) progressClass = 'inactive';
        else if (pct >= prefs.red_threshold) progressClass = 'high';
        else if (pct >= prefs.yellow_threshold) progressClass = 'medium';

        let statusText = '';
        if (!avail) statusText = 'N/A';
        else if (p.usage_unit === 'Status') statusText = 'OK';
        else statusText = displayPct.toFixed(0) + '%';

        let statusDetail = '';
        if (avail && p.cost_used > 0 && !privacyMode) {
            statusDetail = '$' + p.cost_used.toFixed(2);
        } else if (avail && p.usage_unit !== 'Status' && !privacyMode) {
            statusDetail = pct.toFixed(0) + '%';
        } else {
            statusDetail = desc || '';
        }

        let resetText = '';
        if (p.next_reset_time) {
            resetText = window.AicUtils.formatResetDisplay(p.next_reset_time);
        }

        let displayName = p.provider_name;
        let accountName = '';
        if (p.provider_id === 'github-copilot') {
            accountName = githubAuthStatus.username || p.account_name || '';
        } else {
            accountName = p.account_name || '';
        }
        if (accountName && accountName.trim() !== '') {
            const accountDisplay = privacyMode ? '***' : window.AicUtils.escapeHtml(accountName);
            displayName = window.AicUtils.escapeHtml(p.provider_name) + ' [' + accountDisplay + ']';
        } else {
            displayName = window.AicUtils.escapeHtml(p.provider_name);
        }

        providerHtml += '<div class="provider-item compact' + (avail ? '' : ' unavailable') + '">' +
            (avail && width > 0 ? '<div class="provider-progress-bg ' + progressClass + '" style="width:' + width + '%"></div>' : '') +
            '<div class="provider-content">' +
            '<span class="provider-name">' + displayName + '</span>' +
            '<span style="flex:1;"></span>' +
            '<span class="provider-status">' + window.AicUtils.escapeHtml(statusText) + (statusDetail ? ' (' + window.AicUtils.escapeHtml(statusDetail) + ')' : '') + '</span>' +
            (resetText ? '<span class="provider-status" style="color:#FFD700;font-weight:600;margin-left:10px;">' + window.AicUtils.escapeHtml(resetText) + '</span>' : '') +
            '</div></div>';

        if (p.details && p.details.length > 0 && avail) {
            const isSubCollapsed = getProviderCollapsed(p.provider_id);
            const subCount = p.details.length;

            providerHtml += '<div class="sub-providers-header" data-provider="' + p.provider_id + '" style="display: flex; align-items: center; margin-top: 4px; margin-left: 16px; cursor: pointer; user-select: none; padding: 2px 0;">' +
                '<span class="sub-toggle" style="font-size: 8px; margin-right: 6px; color: #808080; transition: transform 0.2s;">' + (isSubCollapsed ? '▶' : '▼') + '</span>' +
                '<span style="font-size: 9px; color: #808080;">' + subCount + ' sub providers</span>' +
                '</div>' +
                '<div class="sub-providers-content" data-provider="' + p.provider_id + '" style="display: ' + (isSubCollapsed ? 'none' : 'block') + ';">';

            p.details.forEach(detail => {
                let detailUsedPct = 0;
                if (detail.used) {
                    const match = detail.used.match(/(\d+)/);
                    if (match) detailUsedPct = parseInt(match[1]);
                }

                let detailRemainingPct = detail.remaining !== undefined ? detail.remaining : Math.max(0, 100 - detailUsedPct);
                let detailText = detailRemainingPct + '%';
                let detailDisplayPct = detailRemainingPct;

                let detailCls = 'low';
                if (detailUsedPct >= prefs.red_threshold) detailCls = 'high';
                else if (detailUsedPct >= prefs.yellow_threshold) detailCls = 'medium';

                let detailResetText = '';
                if (detail.next_reset_time) {
                    detailResetText = window.AicUtils.formatResetDisplay(detail.next_reset_time);
                }

                let usedText = detailUsedPct + '%';
                providerHtml += '<div class="provider-item compact child" style="margin-left:16px;height:18px;padding:2px 6px;">' +
                    (detailDisplayPct > 0 ? '<div class="provider-progress-bg ' + detailCls + '" style="width:' + detailDisplayPct + '%;opacity:0.25;display:flex;align-items:center;justify-content:flex-start;padding-left:4px;">' +
                    '<span style="font-size:8px;color:#fff;font-weight:600;text-shadow:0 0 2px #000;">' + window.AicUtils.escapeHtml(usedText) + '</span>' +
                    '</div>' : '') +
                    '<div class="provider-content" style="font-size:9px;">' +
                    '<span class="provider-name" style="font-size:10px;color:#C0C0C0;">' + window.AicUtils.escapeHtml(detail.name) + '</span>' +
                    '<span style="flex:1;"></span>' +
                    '<span class="provider-status" style="font-size:9px;">' + window.AicUtils.escapeHtml(detailText) + '</span>' +
                    (detailResetText ? '<span class="provider-status" style="color:#FFD700;font-weight:600;font-size:9px;margin-left:8px;">' + window.AicUtils.escapeHtml(detailResetText) + '</span>' : '') +
                    '</div></div>';
            });

            providerHtml += '</div>';
        }
        return providerHtml;
    };

    let html = '';

    if (quotaItems.length > 0) {
        html += createGroupHeader('Plans & Quotas', '#00BFFF', 'plans');
        quotaItems.forEach(p => html += renderProvider(p));
        html += '</div>';
    }

    if (payGoItems.length > 0) {
        html += createGroupHeader('Pay As You Go', '#3CB371', 'paygo');
        payGoItems.forEach(p => html += renderProvider(p));
        html += '</div>';
    }

    container.innerHTML = html;

    // Add click handlers
    document.querySelectorAll('.group-header').forEach(header => {
        header.addEventListener('click', function () {
            const groupId = this.getAttribute('data-group');
            const content = document.querySelector(`.group-content[data-group="${groupId}"]`);
            const toggle = this.querySelector('.group-toggle');
            const title = this.querySelector('span:nth-child(2)').textContent;

            if (content) {
                const isCollapsed = content.style.display === 'none';
                content.style.display = isCollapsed ? 'block' : 'none';
                toggle.textContent = isCollapsed ? '▼' : '▶';
                this.title = `Click to ${isCollapsed ? 'collapse' : 'expand'}`;

                const storageKey = groupId === 'plans' ? 'group_collapsed_plans' : 'group_collapsed_paygo';
                localStorage.setItem(storageKey, (!isCollapsed).toString());
            }
        });
    });

    document.querySelectorAll('.sub-providers-header').forEach(header => {
        header.addEventListener('click', function () {
            const providerId = this.getAttribute('data-provider');
            const content = document.querySelector(`.sub-providers-content[data-provider="${providerId}"]`);
            const toggle = this.querySelector('.sub-toggle');

            if (content) {
                const isCollapsed = content.style.display === 'none';
                content.style.display = isCollapsed ? 'block' : 'none';
                toggle.textContent = isCollapsed ? '▼' : '▶';
                setProviderCollapsed(providerId, !isCollapsed);
            }
        });
    });

    agentConnected = true;
    updateAgentUI('connected', 'Agent Connected');
    updateCachedBadge();
    applyFontSettings();

    const totalTime = Date.now() - window.appStartTime;
    console.log('[TIMING] UI_START_TO_DATA_DISPLAYED: ' + totalTime + 'ms');

    startPeriodicStatusCheck();
}

/**
 * Toggle privacy mode
 */
async function togglePrivacy() {
    privacyMode = !privacyMode;
    document.getElementById('privacyToggleBtn').classList.toggle('active', privacyMode);

    const btn = document.getElementById('privacyToggleBtn');
    btn.title = privacyMode ? 'Privacy Mode On (Click to Show)' : 'Privacy Mode Off (Click to Hide)';

    renderProviders();

    await window.AicUtils.setPrivacyMode(privacyMode);
}

/**
 * Toggle always on top
 */
async function toggleAlwaysOnTop() {
    const enabled = document.getElementById('alwaysOnTopToggle').checked;

    if (invoke) {
        try {
            await invoke('toggle_always_on_top', { enabled });
            console.log('Always on top set to:', enabled);
        } catch (e) {
            console.error('Failed to set always on top:', e);
        }
    }

    try {
        localStorage.setItem('always_on_top', JSON.stringify(enabled));
    } catch (e) {
        console.warn('Could not save to localStorage:', e);
    }
}

/**
 * Open agent management
 */
function openAgentManagement() {
    console.log('Opening agent management...');
    if (invoke) {
        invoke('open_agent_window')
            .then(() => console.log('Agent management opened'))
            .catch(e => console.error('Failed to open agent management:', e));
    } else {
        console.error('Tauri not available');
        alert('Agent management cannot be opened. Tauri API not available.');
    }
}

/**
 * Toggle agent (start/stop)
 */
async function toggleAgent() {
    console.log('Toggling agent... Current state:', agentConnected ? 'running' : 'stopped');
    if (agentConnected) {
        await stopAgentFromMain();
    } else {
        await startAgentFromMain();
    }
}

/**
 * Stop agent from main window
 */
async function stopAgentFromMain() {
    console.log('Stopping agent from main window...');
    if (invoke) {
        await updateAgentUI('connecting', 'Stopping Agent...');
        try {
            const success = await invoke('stop_agent');
            if (success) {
                console.log('Agent stopped successfully');
                agentConnected = false;
                await updateAgentUI('disconnected', 'Agent Stopped');
                document.getElementById('providersList').innerHTML = '<div class="loading">Waiting for agent...</div>';
            }
        } catch (e) {
            console.error('Failed to stop agent:', e);
            const errorDetails = e?.message || e?.toString() || 'Unknown error';
            await updateAgentUI('error', 'Failed to Stop Agent', errorDetails);
        }
    }
}

/**
 * Start agent from main window
 */
async function startAgentFromMain() {
    console.log('Starting agent from main window...');
    if (invoke) {
        await updateAgentUI('connecting', 'Starting Agent...');
        try {
            const success = await invoke('start_agent');
            if (success) {
                console.log('Agent started successfully');
                setTimeout(checkAgentStatus, 2000);
            }
        } catch (e) {
            console.error('Failed to start agent:', e);
            let errorMessage = 'Unknown error';
            let errorDetails = '';

            if (e && typeof e === 'object') {
                if (e.message) errorMessage = e.message;
                else if (e.error) errorMessage = e.error;
            } else if (typeof e === 'string') {
                errorMessage = e;
            }

            if (errorMessage.includes('not found') || errorMessage.includes('executable')) {
                errorDetails = `Agent executable not found.\n\nTo fix this:\n1. Open terminal in the rust directory\n2. Run: cargo build -p aic_agent\n3. Then click the 🤖 button again`;
            } else if (errorMessage.includes('port') || errorMessage.includes('8080')) {
                errorDetails = `Port 8080 is already in use.\n\nAnother instance of the agent may be running.\nTry refreshing the data instead.`;
            } else if (errorMessage.includes('permission') || errorMessage.includes('access')) {
                errorDetails = `Permission denied.\n\nTry running the application with administrator privileges.`;
            } else {
                errorDetails = errorMessage;
            }

            await updateAgentUI('error', 'Failed to Start Agent', errorDetails);
        }
    }
}

/**
 * Update agent UI elements
 */
async function updateAgentUI(status, message, errorDetails = '') {
    const agentBtn = document.getElementById('agentBtn');
    const statusIndicator = document.getElementById('statusIndicator');
    const statusText = document.getElementById('statusText');
    const container = document.getElementById('providersList');

    agentConnected = (status === 'connected');

    if (agentBtn) {
        if (status === 'connected') {
            agentBtn.disabled = false;
            try {
                const agentVersion = await invoke('get_agent_version');
                agentBtn.title = `Agent Running (v${agentVersion}) - Click to Stop`;
            } catch (e) {
                agentBtn.title = 'Agent Running - Click to Stop';
            }
            agentBtn.style.opacity = '1';
        } else if (status === 'disconnected') {
            agentBtn.disabled = false;
            agentBtn.title = 'Start Agent';
            agentBtn.style.opacity = '1';
        } else if (status === 'error') {
            agentBtn.disabled = false;
            agentBtn.title = 'Click to Retry';
            agentBtn.style.opacity = '1';
        } else {
            agentBtn.disabled = false;
            agentBtn.title = 'Start Agent';
            agentBtn.style.opacity = '1';
        }
    }

    if (statusIndicator) {
        if (status === 'error') {
            statusIndicator.className = 'status-indicator disconnected';
        } else {
            statusIndicator.className = 'status-indicator ' + status;
        }
    }

    if (statusText) {
        statusText.textContent = message;
    }

    if (container) {
        if (status === 'connected') {
            if (usageData.length === 0) {
                loadData();
            }
        } else if (status === 'error' && errorDetails) {
            container.innerHTML =
                '<div class="error-message">' +
                '<div class="error-title">⚠️ ' + window.AicUtils.escapeHtml(message) + '</div>' +
                '<div class="error-details">' + window.AicUtils.escapeHtml(errorDetails) + '</div>' +
                '<div class="error-actions">' +
                '<button onclick="startAgentFromMain()">🤖 Retry Start</button>' +
                '<button onclick="checkAgentStatus()">🔄 Check Status</button>' +
                '</div>' +
                '</div>';
        } else if (!freshDataArrived && !isShowingCachedData) {
            container.innerHTML = '<div class="loading">' + window.AicUtils.escapeHtml(message) + '</div>';
        }
    }
}

/**
 * Check and broadcast agent status
 */
async function checkAndBroadcastAgentStatus() {
    if (!invoke) {
        await updateAgentUI('disconnected', 'Not Connected');
        return;
    }

    try {
        const status = await invoke('get_agent_status');
        console.log('Agent status:', status);

        if (window.__TAURI__?.event) {
            try {
                await window.__TAURI__.event.emit('agent-status-changed', status);
            } catch (e) {
                console.error('Failed to emit agent status:', e);
            }
        }

        return status;
    } catch (e) {
        console.error('Failed to check agent status:', e);
        return { status: 'disconnected', message: 'Connection Error', is_running: false };
    }
}

/**
 * Setup agent status listener
 */
function setupAgentStatusListener() {
    if (window.__TAURI__?.event) {
        window.__TAURI__.event.listen('agent-status-changed', async (event) => {
            const status = event.payload;
            console.log('Received agent status from other window:', status);

            if (status.status === 'connected') {
                agentConnected = true;
                await updateAgentUI('connected', status.message);
                if (!freshDataArrived) {
                    console.log('[CACHE] Agent connected, refreshing data...');
                    loadUsageDataAsync();
                }
            } else if (status.status === 'connecting') {
                await updateAgentUI('connecting', status.message);
            } else {
                agentConnected = false;
                await updateAgentUI('disconnected', status.message);
            }
        });
    }
}

/**
 * Check agent status
 */
async function checkAgentStatus() {
    const status = await checkAndBroadcastAgentStatus();
    if (status.status === 'connected') {
        await updateAgentUI('connected', status.message);
    } else if (status.status === 'connecting') {
        await updateAgentUI('connecting', status.message);
    } else {
        await updateAgentUI('disconnected', status.message);
    }
}

/**
 * Start periodic status check
 */
function startPeriodicStatusCheck() {
    if (statusCheckInterval) {
        clearInterval(statusCheckInterval);
    }

    const runCheck = async () => {
        pollingAttempts++;
        await checkAgentStatus();

        if (pollingAttempts === 60 || pollingAttempts === 120) {
            const nextInterval = pollingAttempts >= 120 ? '30s' : '5s';
            console.log(`[POLL] Transitioning to ${nextInterval} polling mode`);
            startPeriodicStatusCheck();
        }
    };

    let interval;
    if (pollingAttempts < 60) {
        interval = 1000;
    } else if (pollingAttempts < 120) {
        interval = 5000;
    } else {
        interval = 30000;
    }

    statusCheckInterval = setInterval(runCheck, interval);
}

/**
 * Setup agent ready listener
 */
function setupAgentReadyListener() {
    if (window.__TAURI__?.event) {
        window.__TAURI__.event.listen('agent-ready', async () => {
            console.log('[EVENT] Received agent-ready event, triggering streaming data refresh');
            pollingAttempts = 0;
            startPeriodicStatusCheck();
            requestStreamingData();
        });
    }
}

/**
 * Request streaming data from backend
 */
function requestStreamingData() {
    if (invoke) {
        console.log('[STREAM] Requesting streaming data from backend');
        invoke('stream_ui_data').catch(e => console.error('[STREAM] stream_ui_data failed:', e));
    }
}

/**
 * Setup streaming listeners for data events
 */
function setupStreamingListeners() {
    if (!window.__TAURI__?.event) return;

    window.__TAURI__.event.listen('ui-data-usage', (event) => {
        const newUsageData = event.payload;
        console.log('[STREAM] Received ui-data-usage:', newUsageData?.length, 'providers at', (Date.now() - window.appStartTime) + 'ms');
        if (newUsageData && newUsageData.length > 0) {
            freshDataArrived = true;
            isShowingCachedData = false;
            usageData = newUsageData;
            window.AicUtils.saveUsageDataToCache(usageData);
            renderProviders();
            updateAgentUI('connected', 'Live');
            if (invoke) {
                invoke('set_data_live', { isLive: true })
                    .catch(e => console.error('Failed to set data live:', e));
            }
        }
    });

    window.__TAURI__.event.listen('ui-data-github', (event) => {
        const auth = event.payload;
        console.log('[STREAM] Received ui-data-github:', auth);
        if (auth) {
            githubAuthStatus = auth;
            if (usageData.length > 0) {
                renderProviders();
            }
        }
    });

    window.__TAURI__.event.listen('ui-data-agent-info', (event) => {
        console.log('[STREAM] Received ui-data-agent-info:', event.payload);
        window._agentInfo = event.payload;
    });
}

/**
 * Setup data status listener
 */
function setupDataStatusListener() {
    if (window.__TAURI__?.event) {
        window.__TAURI__.event.listen('data-status-changed', async (event) => {
            console.log('[CACHE] Received data-status-changed event:', event.payload);
            await updateCachedBadge();
        });
    }
}

/**
 * Setup font settings listener
 */
function setupFontSettingsListener() {
    if (window.__TAURI__?.event) {
        window.__TAURI__.event.listen('font-settings-changed', (event) => {
            console.log('Received font-settings-changed event:', event.payload);
            prefs.font_family = event.payload.font_family;
            prefs.font_size = event.payload.font_size;
            prefs.font_bold = event.payload.font_bold;
            prefs.font_italic = event.payload.font_italic;
            applyFontSettings();
            renderProviders();
        });
    }
}

/**
 * Setup window focus listener
 */
function setupFocusListener() {
    if (window.__TAURI__?.event) {
        window.__TAURI__.event.listen('tauri://focus', async () => {
            console.log('Window focused - syncing badge state');
            await updateCachedBadge();
        });
    }
}

/**
 * Update cached badge
 */
async function updateCachedBadge() {
    const cachedBadge = document.getElementById('cachedBadge');
    if (!cachedBadge) {
        console.log('[CACHE] Badge element not found');
        return;
    }

    try {
        const status = await invoke('get_data_status');
        console.log('[CACHE] updateCachedBadge - Rust says:', status, 'usageData length:', usageData?.length);

        if (usageData && usageData.length > 0) {
            const label = (status.label === 'Live' && !freshDataArrived) ? 'Cached' : status.label;
            const css_class = (status.label === 'Live' && !freshDataArrived) ? 'cached' : (status.css_class || '');

            cachedBadge.textContent = label;
            cachedBadge.className = 'cached-badge ' + css_class;
            cachedBadge.style.display = 'inline-block';
            console.log('[CACHE] Showing badge:', label, '(Rust said:', status.label, 'freshDataArrived:', freshDataArrived, ')');
        } else {
            cachedBadge.style.display = 'none';
            console.log('[CACHE] Hiding badge (no data)');
        }
    } catch (e) {
        console.log('[CACHE] Could not query data status:', e);
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
 * Load usage data asynchronously
 */
async function loadUsageDataAsync() {
    const loadStartTime = Date.now();
    const container = document.getElementById('providersList');
    console.log('[TIMING] FETCH_FROM_AGENT_START: ' + (loadStartTime - window.appStartTime) + 'ms');
    try {
        let uiData = null;
        if (invoke) {
            uiData = await invoke('get_all_ui_data');
            const elapsed = Date.now() - loadStartTime;
            console.log('[TIMING] FETCH_FROM_AGENT_DONE: ' + elapsed + 'ms');
            console.log('[TIMING] UI Data:', uiData);
            console.log('[TIMING] usage count:', uiData?.usage?.length);
        }

        const newUsageData = uiData?.usage;
        console.log('[CACHE] Checking fresh data: newUsageData =', typeof newUsageData, 'length =', newUsageData?.length);

        if (newUsageData && newUsageData.length > 0) {
            freshDataArrived = true;
            isShowingCachedData = false;
            usageData = newUsageData;
            console.log('[CACHE] Got FRESH data with', newUsageData.length, 'providers, calling render');
            window.AicUtils.saveUsageDataToCache(usageData);
        } else {
            console.log('[CACHE] Got empty data from agent, keeping cached data');
        }

        console.log('[CACHE] About to call renderProviders, freshDataArrived:', freshDataArrived, 'isShowingCachedData:', isShowingCachedData);
        renderProviders();

        if (freshDataArrived && invoke) {
            invoke('set_data_live', { isLive: true })
                .catch(e => console.error('Failed to set data live:', e));
        }

    } catch (err) {
        console.error('Error loading usage data:', err);
        console.log('[CACHE] Error fetching data, keeping cached data');
        setTimeout(() => {
            console.log('[CACHE] Retrying data fetch...');
            loadUsageDataAsync();
        }, 5000);
    }
}

/**
 * Open settings window
 */
function openSettings() {
    console.log('Opening settings...');
    if (invoke) {
        invoke('open_settings_window')
            .then(() => console.log('Settings opened'))
            .catch(e => console.error('Failed to open settings:', e));
    } else {
        console.error('Tauri not available');
        alert('Settings cannot be opened. Tauri API not available.');
    }
}

// Privacy mode change handler for this window
function onPrivacyModeChanged(enabled) {
    renderProviders();
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', initMainWindow);

// Expose functions globally for inline event handlers
window.startAgentFromMain = startAgentFromMain;
window.checkAgentStatus = checkAgentStatus;
window.onPrivacyModeChanged = onPrivacyModeChanged;
