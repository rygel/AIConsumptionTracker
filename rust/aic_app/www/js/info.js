/*
 * AI Consumption Tracker - Info Window JavaScript (info.html)
 */

/**
 * Initialize info window
 */
async function initInfoWindow() {
    console.log('Info window loaded');
    
    setupCloseButtons();
    setupPrivacyModeListener();
    loadPrivacyMode();
    await loadSystemInfo();
}

/**
 * Setup close buttons
 */
function setupCloseButtons() {
    document.getElementById('closeBtn').addEventListener('click', closeWindow);
    document.getElementById('closeBtnFooter').addEventListener('click', closeWindow);
    document.getElementById('privacyToggleBtn').addEventListener('click', togglePrivacyMode);
}

/**
 * Setup privacy mode listener
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
}

/**
 * Toggle privacy mode
 */
async function togglePrivacyMode() {
    window.privacyMode = !window.privacyMode;
    const btn = document.getElementById('privacyToggleBtn');
    btn.classList.toggle('active', window.privacyMode);
    btn.title = window.privacyMode ? 'Privacy Mode On (Click to Show)' : 'Privacy Mode Off (Click to Hide)';
    
    if (window.__TAURI__?.event) {
        try {
            await window.__TAURI__.event.emit('privacy-mode-changed', { enabled: window.privacyMode });
        } catch (e) {
            console.error('Failed to emit privacy mode event:', e);
        }
    }
    
    try {
        localStorage.setItem('privacy_mode', JSON.stringify(window.privacyMode));
    } catch (e) {
        console.warn('Could not save privacy mode:', e);
    }
}

/**
 * Close window
 */
async function closeWindow() {
    if (window.__TAURI__?.core?.invoke) {
        await window.__TAURI__.core.invoke('close_info_window');
    } else if (window.__TAURI__?.invoke) {
        await window.__TAURI__.invoke('close_info_window');
    } else {
        window.close();
    }
}

/**
 * Load system info
 */
async function loadSystemInfo() {
    try {
        document.getElementById('versionText').textContent = 'v' + await window.__TAURI__.core.invoke('get_app_version');
        
        document.getElementById('rustVersion').textContent = 'Rust ' + navigator.userAgent;
        document.getElementById('osVersion').textContent = navigator.platform;
        document.getElementById('archText').textContent = navigator.hardwareConcurrency + ' cores';
        document.getElementById('machineName').textContent = navigator.platform;
        document.getElementById('userName').textContent = 'User';
        
        if (window.__TAURI__?.core?.invoke) {
            const configPath = await window.__TAURI__.core.invoke('get_config_path');
            document.getElementById('configPath').textContent = configPath || 'auth.json';
        }
    } catch (e) {
        console.error('Failed to load system info:', e);
    }
}

// Privacy mode change handler for this window
function onPrivacyModeChanged(enabled) {
    window.privacyMode = enabled;
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', initInfoWindow);

// Expose for global access
window.onPrivacyModeChanged = onPrivacyModeChanged;
