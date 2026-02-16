// Theme Management System with HTMX Support
// AI Consumption Tracker Web UI

const ThemeManager = {
    // Available themes
    themes: [
        { id: 'dark', name: 'Dark', icon: '🌙' },
        { id: 'light', name: 'Light', icon: '☀️' },
        { id: 'high-contrast', name: 'High Contrast', icon: '👁️' },
        { id: 'solarized-dark', name: 'Solarized Dark', icon: '🌆' },
        { id: 'solarized-light', name: 'Solarized Light', icon: '🌅' },
        { id: 'dracula', name: 'Dracula', icon: '🧛' },
        { id: 'nord', name: 'Nord', icon: '❄️' }
    ],

    // Initialize theme system
    init() {
        this.loadSavedTheme();
        this.setupThemeDropdown();
        this.setupHTMXListeners();
        this.applyTheme(this.getCurrentTheme());
    },

    // Get current theme from localStorage or default
    getCurrentTheme() {
        return localStorage.getItem('theme') || 'dark';
    },

    // Apply theme to document
    applyTheme(themeId) {
        const html = document.documentElement;
        html.setAttribute('data-theme', themeId);
        this.updateThemeDisplay(themeId);
        localStorage.setItem('theme', themeId);
        
        // Update active state in dropdown
        this.updateDropdownActiveState(themeId);
        
        // Dispatch custom event for other components
        window.dispatchEvent(new CustomEvent('themeChanged', { detail: { theme: themeId } }));
    },

    // Set specific theme
    setTheme(themeId) {
        if (this.themes.find(t => t.id === themeId)) {
            this.applyTheme(themeId);
        }
    },

    // Update theme display in dropdown button
    updateThemeDisplay(themeId) {
        const theme = this.themes.find(t => t.id === themeId);
        const iconElement = document.getElementById('current-theme-icon');
        const nameElement = document.getElementById('current-theme-name');
        
        if (iconElement && theme) {
            iconElement.textContent = theme.icon;
        }
        
        if (nameElement && theme) {
            nameElement.textContent = theme.name;
        }
    },

    // Update active state in dropdown
    updateDropdownActiveState(themeId) {
        const options = document.querySelectorAll('.theme-option');
        options.forEach(option => {
            option.classList.remove('active');
            if (option.dataset.theme === themeId) {
                option.classList.add('active');
            }
        });
    },

    // Setup theme dropdown
    setupThemeDropdown() {
        const dropdownBtn = document.getElementById('theme-dropdown-btn');
        const dropdownMenu = document.getElementById('theme-dropdown-menu');
        
        if (!dropdownBtn || !dropdownMenu) return;
        
        // Toggle dropdown
        dropdownBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            const isOpen = dropdownMenu.classList.contains('show');
            
            if (isOpen) {
                this.closeDropdown();
            } else {
                this.openDropdown();
            }
        });
        
        // Theme option clicks
        const themeOptions = dropdownMenu.querySelectorAll('.theme-option');
        themeOptions.forEach(option => {
            option.addEventListener('click', (e) => {
                e.stopPropagation();
                const themeId = option.dataset.theme;
                this.setTheme(themeId);
                this.closeDropdown();
                this.showNotification(`Theme: ${this.getThemeName(themeId)}`);
            });
        });
        
        // Close dropdown when clicking outside
        document.addEventListener('click', () => {
            this.closeDropdown();
        });
        
        // Update initial display
        this.updateThemeDisplay(this.getCurrentTheme());
        this.updateDropdownActiveState(this.getCurrentTheme());
    },

    // Open dropdown
    openDropdown() {
        const dropdownBtn = document.getElementById('theme-dropdown-btn');
        const dropdownMenu = document.getElementById('theme-dropdown-menu');
        
        if (dropdownMenu) {
            dropdownMenu.classList.add('show');
        }
        if (dropdownBtn) {
            dropdownBtn.classList.add('open');
        }
    },

    // Close dropdown
    closeDropdown() {
        const dropdownBtn = document.getElementById('theme-dropdown-btn');
        const dropdownMenu = document.getElementById('theme-dropdown-menu');
        
        if (dropdownMenu) {
            dropdownMenu.classList.remove('show');
        }
        if (dropdownBtn) {
            dropdownBtn.classList.remove('open');
        }
    },

    // Get theme name
    getThemeName(themeId) {
        const theme = this.themes.find(t => t.id === themeId);
        return theme ? theme.name : themeId;
    },

    // Load saved theme on page load
    loadSavedTheme() {
        const savedTheme = localStorage.getItem('theme');
        if (savedTheme) {
            document.documentElement.setAttribute('data-theme', savedTheme);
        }
    },

    // Setup HTMX event listeners for theme persistence across swaps
    setupHTMXListeners() {
        // Re-apply theme after HTMX content swaps
        document.addEventListener('htmx:afterSwap', () => {
            this.updateThemeDisplay(this.getCurrentTheme());
            this.updateDropdownActiveState(this.getCurrentTheme());
        });
    },

    // Show temporary notification
    showNotification(message) {
        // Remove existing notification
        const existing = document.getElementById('theme-notification');
        if (existing) {
            existing.remove();
        }
        
        // Create new notification
        const notification = document.createElement('div');
        notification.id = 'theme-notification';
        notification.style.cssText = `
            position: fixed;
            bottom: 20px;
            right: 20px;
            background-color: var(--bg-secondary);
            color: var(--text-primary);
            padding: 12px 20px;
            border-radius: 8px;
            border: 1px solid var(--border-color);
            box-shadow: 0 4px 12px var(--shadow-color);
            z-index: 10000;
            font-size: 14px;
            opacity: 0;
            transform: translateY(20px);
            transition: opacity 0.3s, transform 0.3s;
        `;
        notification.textContent = message;
        
        document.body.appendChild(notification);
        
        // Animate in
        requestAnimationFrame(() => {
            notification.style.opacity = '1';
            notification.style.transform = 'translateY(0)';
        });
        
        // Remove after delay
        setTimeout(() => {
            notification.style.opacity = '0';
            notification.style.transform = 'translateY(20px)';
            setTimeout(() => notification.remove(), 300);
        }, 2000);
    }
};

// Initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => ThemeManager.init());
} else {
    ThemeManager.init();
}

// Expose to global scope for debugging
window.ThemeManager = ThemeManager;
