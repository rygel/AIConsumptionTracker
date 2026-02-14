/*
 * AI Consumption Tracker - Shared Utilities
 * Common functions used across all windows
 */

// Tauri invoke helper - centralized to prevent reference errors
const invoke = window.__TAURI__?.invoke || window.__TAURI__?.core?.invoke;

// Cache key constants
const CACHE_KEY_USAGE_DATA = 'cached_usage_data';
const CACHE_KEY_PRIVACY_MODE = 'privacy_mode';
const CACHE_KEY_ALWAYS_ON_TOP = 'always_on_top';

// Default port
const DEFAULT_AGENT_PORT = 8080;

// Track app start time for timing measurement
window.appStartTime = Date.now();

/**
 * Escape HTML special characters to prevent XSS
 * @param {string} text - Text to escape
 * @returns {string} Escaped text
 */
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/**
 * Load cached usage data from localStorage
 * @returns {Array|null} Cached usage data or null
 */
function loadCachedUsageData() {
    try {
        const cached = localStorage.getItem(CACHE_KEY_USAGE_DATA);
        if (cached) {
            const data = JSON.parse(cached);
            if (data && Array.isArray(data) && data.length > 0) {
                console.log('[CACHE] Loaded cached usage data:', data.length, 'providers');
                return data;
            }
        }
    } catch (e) {
        console.log('[CACHE] Could not load cached data:', e);
    }
    return null;
}

/**
 * Save usage data to localStorage cache
 * @param {Array} data - Usage data to cache
 */
function saveUsageDataToCache(data) {
    try {
        localStorage.setItem(CACHE_KEY_USAGE_DATA, JSON.stringify(data));
        console.log('[CACHE] Saved usage data to cache');
    } catch (e) {
        console.log('[CACHE] Could not save cache:', e);
    }
}

/**
 * Get the agent port, with caching
 * @returns {Promise<number>} Agent port
 */
async function getAgentPort() {
    // Check if we already have it cached globally
    if (window.cachedAgentPort !== undefined) {
        return window.cachedAgentPort;
    }

    try {
        if (invoke) {
            const port = await invoke('get_agent_port_cmd');
            if (port && port > 0) {
                window.cachedAgentPort = port;
                return port;
            }
        }
    } catch (e) {
        console.log('Could not get port from backend:', e);
    }
    return DEFAULT_AGENT_PORT;
}

/**
 * Load privacy mode from localStorage
 * @returns {boolean} Privacy mode state
 */
function loadPrivacyMode() {
    try {
        const saved = localStorage.getItem(CACHE_KEY_PRIVACY_MODE);
        if (saved !== null) {
            return JSON.parse(saved);
        }
    } catch (e) {
        console.warn('Could not load privacy mode:', e);
    }
    return false;
}

/**
 * Save privacy mode to localStorage
 * @param {boolean} enabled - Privacy mode state
 */
function savePrivacyMode(enabled) {
    try {
        localStorage.setItem(CACHE_KEY_PRIVACY_MODE, JSON.stringify(enabled));
    } catch (e) {
        console.warn('Could not save privacy mode to localStorage:', e);
    }
}

/**
 * Toggle privacy mode and emit event to sync windows
 * @param {boolean} enabled - New privacy mode state
 * @returns {Promise<void>}
 */
async function setPrivacyMode(enabled) {
    window.privacyMode = enabled;
    
    // Update button if it exists
    const btn = document.getElementById('privacyToggleBtn');
    if (btn) {
        btn.classList.toggle('active', enabled);
        btn.title = enabled ? 'Privacy Mode On (Click to Show)' : 'Privacy Mode Off (Click to Hide)';
    }

    // Emit event to sync with other windows
    if (window.__TAURI__?.event) {
        try {
            await window.__TAURI__.event.emit('privacy-mode-changed', { enabled });
        } catch (e) {
            console.error('Failed to emit privacy mode event:', e);
        }
    }

    // Save to localStorage
    savePrivacyMode(enabled);
}

/**
 * Setup privacy mode listener for cross-window sync
 */
function setupPrivacyModeListener() {
    if (window.__TAURI__?.event) {
        window.__TAURI__.event.listen('privacy-mode-changed', (event) => {
            console.log('Received privacy mode change:', event);
            const newPrivacyMode = event.payload?.enabled ?? false;
            if (newPrivacyMode !== window.privacyMode) {
                window.privacyMode = newPrivacyMode;
                
                const btn = document.getElementById('privacyToggleBtn');
                if (btn) {
                    btn.classList.toggle('active', newPrivacyMode);
                    btn.title = newPrivacyMode ? 'Privacy Mode On (Click to Show)' : 'Privacy Mode Off (Click to Hide)';
                }
                
                // Call the window-specific handler if it exists
                if (typeof onPrivacyModeChanged === 'function') {
                    onPrivacyModeChanged(newPrivacyMode);
                }
                
                savePrivacyMode(newPrivacyMode);
            }
        });
    }
}

/**
 * Get cached agent port (synchronous)
 * @returns {number} Cached port or default
 */
function getCachedAgentPort() {
    return window.cachedAgentPort || DEFAULT_AGENT_PORT;
}

/**
 * Format reset time display (relative and absolute)
 * @param {string} nextReset - UTC timestamp
 * @returns {string} Formatted reset display
 */
function formatResetDisplay(nextReset) {
    if (!nextReset) return '';

    const resetDate = new Date(nextReset);
    const now = new Date();
    const diffMs = resetDate - now;

    if (diffMs <= 0) return '(Resets: Ready)';

    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    let relative = '';
    if (diffDays >= 1) {
        const remainingHours = Math.floor((diffMs % 86400000) / 3600000);
        relative = `${diffDays}d ${remainingHours}h`;
    } else if (diffHours >= 1) {
        const remainingMins = Math.floor((diffMs % 3600000) / 60000);
        relative = `${diffHours}h ${remainingMins}m`;
    } else {
        relative = `${diffMins}m`;
    }

    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const month = months[resetDate.getMonth()];
    const day = resetDate.getDate().toString().padStart(2, '0');
    const hours = resetDate.getHours().toString().padStart(2, '0');
    const mins = resetDate.getMinutes().toString().padStart(2, '0');
    const absolute = `${month} ${day} ${hours}:${mins}`;

    return `(Resets: ${relative} - ${absolute})`;
}

/**
 * Mask content in privacy mode
 * @param {string} text - Text to mask
 * @param {string} accountName - Account name to keep visible
 * @returns {string} Masked or original text
 */
function maskContent(text, accountName) {
    if (!text) return '';
    if (accountName && text.includes(accountName)) {
        return text;
    }
    return '***';
}

// Provider name formatter
const PROVIDER_NAMES = {
    'github-copilot': 'GitHub Copilot',
    'openai': 'OpenAI',
    'anthropic': 'Anthropic',
    'deepseek': 'DeepSeek',
    'gemini': 'Google Gemini',
    'kimi': 'Kimi'
};

/**
 * Format provider ID to display name
 * @param {string} id - Provider ID
 * @returns {string} Formatted name
 */
function formatProviderName(id) {
    if (PROVIDER_NAMES[id]) return PROVIDER_NAMES[id];
    return id.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
}

// Provider info for settings
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

/**
 * Get provider info by ID
 * @param {string} providerId - Provider ID
 * @returns {object} Provider info object
 */
function getProviderInfo(providerId) {
    return PROVIDER_INFO[providerId] || { 
        name: providerId, 
        icon: providerId.charAt(0).toUpperCase(), 
        color: '#007ACC' 
    };
}

// Export for use in other scripts
window.AicUtils = {
    invoke,
    escapeHtml,
    loadCachedUsageData,
    saveUsageDataToCache,
    getAgentPort,
    loadPrivacyMode,
    savePrivacyMode,
    setPrivacyMode,
    setupPrivacyModeListener,
    getCachedAgentPort,
    formatResetDisplay,
    maskContent,
    formatProviderName,
    getProviderInfo,
    PROVIDER_INFO,
    CACHE_KEY_USAGE_DATA,
    CACHE_KEY_PRIVACY_MODE,
    DEFAULT_AGENT_PORT
};
