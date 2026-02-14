/*
 * HTMX Components for AI Consumption Tracker
 * 
 * This file defines reusable HTMX components that integrate with Tauri.
 * Components use hx-get="javascript:ComponentName.method()" pattern to call
 * Tauri invoke commands through JavaScript functions.
 */

(function() {
    'use strict';

    // ============================================
    // HTMX Helper Functions
    // ============================================

    /**
     * Make a Tauri invoke call and return HTML content
     */
    async function tauriGet(command, params = {}) {
        try {
            const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
            if (!invoke) {
                return '<div class="error">Tauri not available</div>';
            }
            return await invoke(command, params);
        } catch (e) {
            console.error(`HTMX ${command} error:`, e);
            return `<div class="error">Error: ${e}</div>`;
        }
    }

    /**
     * Make a Tauri invoke call for action (non-HTML return)
     */
    async function tauriAction(command, params = {}) {
        try {
            const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
            if (!invoke) {
                return { success: false, error: 'Tauri not available' };
            }
            return await invoke(command, params);
        } catch (e) {
            console.error(`HTMX ${command} action error:`, e);
            return { success: false, error: e };
        }
    }

    // ============================================
    // Provider List Component
    // ============================================

    window.HTMXProviders = {
        /**
         * Load provider usage data
         */
        async load() {
            const container = document.getElementById('providers-list');
            if (container) {
                container.innerHTML = '<div class="loading">Loading providers...</div>';
            }

            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return '<div class="error">Tauri not available</div>';
                }

                // Try to get preloaded data first
                let data = null;
                try {
                    data = await invoke('get_all_ui_data');
                } catch (e) {
                    console.log('Using fallback to agent');
                }

                if (!data || !data.usage) {
                    return '<div class="empty-state">No providers configured</div>';
                }

                return HTMXProviders.render(data.usage, data.github_auth);
            } catch (e) {
                console.error('Failed to load providers:', e);
                return `<div class="error">Failed to load: ${e}</div>`;
            }
        },

        /**
         * Render provider list from data
         */
        render(usageData, githubAuth = {}) {
            if (!usageData || usageData.length === 0) {
                return '<div class="empty-state"><div style="font-size: 24px; margin-bottom: 10px;">📊</div><div>No providers configured</div><div style="font-size: 11px; color: #666; margin-top: 8px;">Use CLI: aic-cli auth &lt;provider&gt;</div></div>';
            }

            const prefs = window.prefs || { yellow_threshold: 60, red_threshold: 80, invert_progress_bar: false };
            const privacyMode = window.privacyMode || false;

            const quotaItems = usageData.filter(u => u.is_quota_based || u.payment_type === 'credits');
            const payGoItems = usageData.filter(u => !u.is_quota_based && u.payment_type !== 'credits');

            const sortByName = (a, b) => a.provider_name.localeCompare(b.provider_name);
            quotaItems.sort(sortByName);
            payGoItems.sort(sortByName);

            let html = '';

            if (quotaItems.length > 0) {
                html += HTMXProviders.renderGroup('Plans & Quotas', '#00BFFF', quotaItems, prefs, privacyMode, githubAuth);
            }

            if (payGoItems.length > 0) {
                html += HTMXProviders.renderGroup('Pay As You Go', '#3CB371', payGoItems, prefs, privacyMode, githubAuth);
            }

            return html;
        },

        /**
         * Render a group of providers
         */
        renderGroup(title, color, items, prefs, privacyMode, githubAuth) {
            let html = `<div class="group-header" style="display: flex; align-items: center; margin: 16px 0 10px 0; cursor: pointer;">
                <span style="font-size: 10px; font-weight: bold; color: ${color}; text-transform: uppercase; letter-spacing: 1px; margin-right: 10px;">${title}</span>
                <div style="flex: 1; height: 1px; background: ${color}; opacity: 0.3;"></div>
            </div>
            <div class="group-content">`;

            items.forEach(p => {
                html += HTMXProviders.renderProvider(p, prefs, privacyMode, githubAuth);
            });

            html += '</div>';
            return html;
        },

        /**
         * Render a single provider
         */
        renderProvider(p, prefs, privacyMode, githubAuth) {
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
            }

            let displayName = p.provider_name;
            let accountName = '';
            
            if (p.provider_id === 'github-copilot') {
                accountName = githubAuth?.username || p.account_name || '';
            } else {
                accountName = p.account_name || '';
            }
            
            if (accountName && accountName.trim() !== '') {
                const accountDisplay = privacyMode ? '***' : escapeHtml(accountName);
                displayName = escapeHtml(p.provider_name) + ' [' + accountDisplay + ']';
            } else {
                displayName = escapeHtml(p.provider_name);
            }

            const width = avail ? Math.min(barWidth, 100) : 0;

            return `<div class="provider-item compact${avail ? '' : ' unavailable'}">
                ${avail && width > 0 ? `<div class="provider-progress-bg ${progressClass}" style="width:${width}%"></div>` : ''}
                <div class="provider-content">
                    <span class="provider-name">${displayName}</span>
                    <span style="flex:1;"></span>
                    <span class="provider-status">${statusText}${statusDetail ? ' (' + statusDetail + ')' : ''}</span>
                </div>
            </div>`;
        },

        /**
         * Refresh providers
         */
        async refresh() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (invoke) {
                    await invoke('refresh_usage_from_agent');
                }
                return await HTMXProviders.load();
            } catch (e) {
                console.error('Refresh error:', e);
                return await HTMXProviders.load();
            }
        }
    };

    // ============================================
    // Agent Status Component
    // ============================================

    window.HTMXAgent = {
        /**
         * Get agent status
         */
        async status() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return { status: 'disconnected', message: 'Tauri not available' };
                }
                return await invoke('get_agent_status');
            } catch (e) {
                return { status: 'disconnected', message: 'Error: ' + e };
            }
        },

        /**
         * Start agent
         */
        async start() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return { success: false, error: 'Tauri not available' };
                }
                return await invoke('start_agent');
            } catch (e) {
                return { success: false, error: e };
            }
        },

        /**
         * Stop agent
         */
        async stop() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return { success: false, error: 'Tauri not available' };
                }
                return await invoke('stop_agent');
            } catch (e) {
                return { success: false, error: e };
            }
        }
    };

    // ============================================
    // Settings Components
    // ============================================

    window.HTMXSettings = {
        /**
         * Load settings data
         */
        async load() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return '<div class="error">Tauri not available</div>';
                }

                const [providers, prefs] = await Promise.all([
                    invoke('get_all_ui_data').catch(() => null),
                    invoke('load_preferences').catch(() => ({}))
                ]);

                return HTMXSettings.renderProviders(providers?.providers || [], providers?.usage || [], prefs, window.githubAuth || {});
            } catch (e) {
                return `<div class="error">Error loading settings: ${e}</div>`;
            }
        },

        /**
         * Render provider settings
         */
        renderProviders(configs, usageData, prefs, githubAuth) {
            if (!configs || configs.length === 0) {
                return '<div class="error">No providers found. Make sure the agent is running.</div>';
            }

            configs.sort((a, b) => {
                const infoA = PROVIDER_INFO[a.provider_id] || { name: a.provider_id };
                const infoB = PROVIDER_INFO[b.provider_id] || { name: b.provider_id };
                return infoA.name.localeCompare(infoB.name);
            });

            return configs.map(config => {
                const info = PROVIDER_INFO[config.provider_id] || { 
                    name: config.provider_id, 
                    icon: config.provider_id.charAt(0).toUpperCase(), 
                    color: '#007ACC' 
                };
                const hasKey = config.api_key && config.api_key.length > 0;
                
                const usage = usageData.find(u => u.provider_id === config.provider_id);
                const isAntigravityConnected = config.provider_id === 'antigravity' && usage && usage.is_available;

                let displayName = info.name;
                let accountName = '';
                
                if (config.provider_id === 'github-copilot') {
                    accountName = githubAuth?.username || '';
                    if (accountName) {
                        displayName = info.name + ' [' + (window.privacyMode ? '***' : accountName) + ']';
                    }
                }

                let authSourceDisplay = config.auth_source || 'None';
                if (authSourceDisplay === 'Environment Variable') authSourceDisplay = 'Env';
                else if (authSourceDisplay === 'Environment') authSourceDisplay = 'Env';
                else if (authSourceDisplay === 'AI Consumption Tracker') authSourceDisplay = 'AICT';

                return `<div class="provider-card" data-provider="${config.provider_id}">
                    <div class="provider-header">
                        <div class="provider-info">
                            <div class="provider-icon" style="background: ${info.color};">${info.icon}</div>
                            <span class="provider-name">${escapeHtml(displayName)}</span>
                        </div>
                        <div class="provider-actions">
                            <span class="auth-source-label">${authSourceDisplay}</span>
                            <label class="checkbox-label">
                                <input type="checkbox" class="tray-checkbox" ${config.show_in_tray ? 'checked' : ''}>
                                <span>Tray</span>
                            </label>
                            <span class="status-badge ${hasKey || isAntigravityConnected ? 'active' : 'inactive'}">${hasKey ? 'Active' : (isAntigravityConnected ? 'Connected' : 'Inactive')}</span>
                        </div>
                    </div>
                    ${config.provider_id === 'antigravity' ? `
                        <div class="provider-row">
                            <span style="color: ${isAntigravityConnected ? '#90EE90' : '#888'}; font-size: 11px;">
                                ${isAntigravityConnected ? 'Running (Connected)' : 'Not Running'}
                            </span>
                        </div>
                    ` : `
                        <div class="provider-row">
                            <input type="text" class="api-key-input" id="api-key-${config.provider_id}"
                                value="${config.api_key || ''}" placeholder="Enter API key"
                                data-provider="${config.provider_id}">
                        </div>
                    `}
                </div>`;
            }).join('');
        },

        /**
         * Save settings
         */
        async save() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return { success: false, error: 'Tauri not available' };
                }

                // Collect provider configs from DOM
                const configs = [];
                document.querySelectorAll('.provider-card').forEach(card => {
                    const providerId = card.dataset.provider;
                    const trayCheckbox = card.querySelector('.tray-checkbox');
                    const apiKeyInput = card.querySelector('.api-key-input');
                    
                    configs.push({
                        provider_id: providerId,
                        show_in_tray: trayCheckbox?.checked || false,
                        api_key: apiKeyInput?.value || ''
                    });
                });

                // Collect preferences
                const prefs = {
                    compact_mode: document.getElementById('compactMode')?.checked || false,
                    always_on_top: document.getElementById('alwaysOnTop')?.checked || false,
                    stay_open: document.getElementById('stayOpen')?.checked || false,
                    show_all: document.getElementById('showAll')?.checked !== false,
                    refresh_interval: parseInt(document.getElementById('refreshInterval')?.value) || 300,
                    yellow_threshold: parseInt(document.getElementById('yellowThreshold')?.value) || 60,
                    red_threshold: parseInt(document.getElementById('redThreshold')?.value) || 80,
                    invert_progress_bar: document.getElementById('invertProgressBar')?.checked || false,
                    auto_update: document.getElementById('autoUpdate')?.checked !== false,
                    font_family: document.getElementById('fontFamily')?.value || '-apple-system',
                    font_size: parseInt(document.getElementById('fontSize')?.value) || 13,
                    font_bold: document.getElementById('fontBold')?.checked || false,
                    font_italic: document.getElementById('fontItalic')?.checked || false
                };

                await invoke('save_provider_configs', { configs });
                await invoke('save_preferences', { preferences: prefs });

                return { success: true };
            } catch (e) {
                return { success: false, error: e };
            }
        }
    };

    // ============================================
    // History Component
    // ============================================

    window.HTMXHistory = {
        /**
         * Load usage history
         */
        async loadUsage() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return '<div class="error">Tauri not available</div>';
                }

                const history = await invoke('get_historical_usage_from_agent', { limit: 50 });
                
                if (!history || history.length === 0) {
                    return '<div class="loading">No history found.</div>';
                }

                let html = `<table class="history-table">
                    <thead>
                        <tr><th>Time</th><th>Provider</th><th>Usage</th><th>Unit</th></tr>
                    </thead>
                    <tbody>`;

                history.forEach(item => {
                    const date = new Date(item.timestamp).toLocaleString();
                    html += `<tr><td>${date}</td><td>${item.provider_name}</td><td>${item.usage?.toFixed(2) || '-'}</td><td>${item.usage_unit || '-'}</td></tr>`;
                });

                html += '</tbody></table>';
                return html;
            } catch (e) {
                return `<div class="error">Error: ${e}</div>`;
            }
        },

        /**
         * Load agent history
         */
        async loadAgent() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return '<div class="error">Tauri not available</div>';
                }

                const now = new Date();
                const thirtyDaysAgo = new Date(now.setDate(now.getDate() - 30));
                const startDate = thirtyDaysAgo.toISOString();

                const history = await invoke('get_historical_usage_from_agent', { limit: 100, startDate });

                if (!history || history.length === 0) {
                    return '<div class="loading">No agent history found. (Retention: 30 days)</div>';
                }

                let html = `<table class="history-table">
                    <thead>
                        <tr><th>Time</th><th>Provider</th><th>Usage</th><th>Limit</th><th>Reset</th></tr>
                    </thead>
                    <tbody>`;

                history.forEach(item => {
                    const date = new Date(item.timestamp * 1000).toLocaleString();
                    html += `<tr>
                        <td style="white-space: nowrap;">${date}</td>
                        <td style="white-space: nowrap;">${item.provider_id}</td>
                        <td>${item.usage !== null ? item.usage : '-'}</td>
                        <td>${item.limit !== null ? item.limit : '-'}</td>
                        <td>${item.next_reset ? new Date(item.next_reset * 1000).toLocaleDateString() : '-'}</td>
                    </tr>`;
                });

                html += '</tbody></table>';
                return html;
            } catch (e) {
                return `<div class="error">Error: ${e}</endiv>`;
            }
        },

        /**
         * Load raw logs
         */
        async loadLogs() {
            try {
                const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;
                if (!invoke) {
                    return '<div class="error">Tauri not available</div>';
                }

                const logs = await invoke('get_raw_responses_from_agent', { limit: 20 });

                if (!logs || logs.length === 0) {
                    return '<div class="loading">No raw logs found. (Retention: 24h)</div>';
                }

                let html = `<table class="history-table">
                    <thead><th>Time</th><th>
                        <tr>Provider</th><th>Response Body</th></tr>
                    </thead>
                    <tbody>`;

                logs.forEach(log => {
                    const date = new Date(log.timestamp * 1000).toLocaleString();
                    html += `<tr>
                        <td style="white-space: nowrap;">${date}</td>
                        <td style="white-space: nowrap;">${log.provider_id}</td>
                        <td><div class="log-body">${escapeHtml(log.response_body)}</div></td>
                    </tr>`;
                });

                html += '</tbody></table>';
                return html;
            } catch (e) {
                return `<div class="error">Error: ${e}</endiv>`;
            }
        }
    };

    // ============================================
    // Provider Info Map
    // ============================================

    const PROVIDER_INFO = {
        'github-copilot': { name: 'GitHub Copilot', icon: 'G', color: '#24292e' },
        'openai': { name: 'OpenAI', icon: 'O', color: '#10a37f' },
        'claude-code': { name: 'Claude Code', icon: 'C', color: '#d4a574' },
        'deepseek': { name: 'DeepSeek', icon: 'D', color: '#1e80ff' },
        'gemini-cli': { name: 'Google Gemini', icon: 'G', color: '#4285f4' },
        'kimi': { name: 'Kimi', icon: 'K', color: '#0066cc' },
        'minimax': { name: 'MiniMax', icon: 'M', color: '#FF6B35' },
        'xiaomi': { name: 'Xiaomi', icon: 'X', color: '#FF6900' },
        'antigravity': { name: 'Antigravity', icon: 'A', color: '#8B5CF6' },
        'openrouter': { name: 'OpenRouter', icon: 'R', color: '#10B981' },
        'zai': { name: 'Z.ai', icon: 'Z', color: '#3B82F6' },
        'zai-coding-plan': { name: 'Z.ai Coding', icon: 'Z', color: '#2563EB' },
        'mistral': { name: 'Mistral', icon: 'M', color: '#F97316' },
        'opencode-zen': { name: 'OpenCode', icon: 'C', color: '#EC4899' },
        'synthetic': { name: 'Synthetic', icon: 'S', color: '#14B8A6' }
    };

    // ============================================
    // Utility Functions
    // ============================================

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Export for global access
    window.HTMX = {
        PROVIDER_INFO,
        escapeHtml
    };

    console.log('HTMX Components loaded');
})();
