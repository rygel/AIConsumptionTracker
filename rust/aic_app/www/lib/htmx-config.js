/*
 * HTMX Configuration for AI Consumption Tracker
 */

// Configure HTMX settings
if (typeof htmx !== 'undefined') {
    htmx.config.historyEnabled = true;
    htmx.config.historyCacheSize = 10;
    htmx.config.refreshOnHistoryMiss = false;
    htmx.config.defaultSwapStyle = 'innerHTML';
    htmx.config.defaultSwapDelay = 0;
    htmx.config.defaultSettleDelay = 20;
    htmx.config.includeIndicatorStyles = true;
    htmx.config.indicatorClass = 'htmx-indicator';
    htmx.config.requestClass = 'htmx-request';
    htmx.config.addedClass = 'htmx-added';
    htmx.config.settlingClass = 'htmx-settling';
    htmx.config.swappingClass = 'htmx-swapping';
    htmx.config.allowEval = true;  // Enable for javascript: protocol
    htmx.config.allowScriptTags = false;
    htmx.config.withCredentials = false;
    htmx.config.timeout = 0;
    htmx.config.disableSelector = '[hx-disable], [data-hx-disable]';
    htmx.config.scrollBehavior = 'instant';
    htmx.config.defaultFocusScroll = false;
    htmx.config.getCacheBusterParam = false;
    htmx.config.globalViewTransitions = false;
    htmx.config.selfRequestsOnly = false;  // Allow javascript: URLs
    htmx.config.ignoreTitle = false;
    htmx.config.scrollIntoViewOnBoost = true;

    // Override the validateUrl function to allow javascript: URLs
    const originalValidateUrl = htmx.spec.validateUrl;
    htmx.spec.validateUrl = function(element, url, details) {
        if (url.startsWith('javascript:')) {
            return true;
        }
        return originalValidateUrl.call(this, element, url, details);
    };
}

/*
 * HTMX Extension: JavaScript Function Handler
 * 
 * This extension intercepts requests with javascript: URLs and 
 * calls the JavaScript function instead of making HTTP requests.
 */
if (typeof htmx !== 'undefined') {
    htmx.defineExtension('js-function', {
        init: function(globals) {
            console.log('HTMX JS Function extension initialized');
        },
        
        getSelectors: function() {
            return null;
        },
        
        onEvent: function(name, evt) {
            return true;
        },
        
        transformResponse: function(e, t, n) {
            return e;
        },
        
        isInlineSwap: function(e) {
            return false;
        },
        
        handleSwap: function(e, t, n, r, o) {
            return false;
        },
        
        encodeParameters: function(e, t, n) {
            return null;
        }
    });
}

// Override htmx.ajax to handle javascript: URLs
if (typeof htmx !== 'undefined') {
    const originalAjax = htmx.ajax;
    
    htmx.ajax = function(verb, path, params) {
        if (typeof path === 'string' && path.startsWith('javascript:')) {
            // Extract function name and parameters
            const jsCode = path.substring('javascript:'.length);
            
            try {
                // Parse the function call
                const match = jsCode.match(/^([A-Za-z_$][A-Za-z0-9_$]*)\.([A-Za-z_$][A-Za-z0-9_$]*)\s*\((.*)\)$/);
                
                if (match) {
                    const objName = match[1];
                    const funcName = match[2];
                    const argsStr = match[3];
                    
                    const obj = window[objName];
                    if (obj && typeof obj[funcName] === 'function') {
                        // Parse arguments
                        let args = [];
                        if (argsStr.trim()) {
                            // Simple argument parsing
                            args = argsStr.split(',').map(arg => {
                                arg = arg.trim();
                                // Handle string literals
                                if ((arg.startsWith('"') && arg.endsWith('"')) || 
                                    (arg.startsWith("'") && arg.endsWith("'"))) {
                                    return arg.slice(1, -1);
                                }
                                // Handle booleans
                                if (arg === 'true') return true;
                                if (arg === 'false') return false;
                                // Handle null
                                if (arg === 'null') return null;
                                // Handle numbers
                                if (!isNaN(arg)) return Number(arg);
                                // Otherwise treat as variable reference
                                return arg;
                            });
                        }
                        
                        // Call the function
                        const result = obj[funcName].apply(obj, args);
                        
                        // If it's a Promise, handle it
                        if (result && typeof result.then === 'function') {
                            return result;
                        }
                        
                        // Return a resolved promise with the result
                        return Promise.resolve(result);
                    }
                }
                
                // Fallback: try eval for complex cases
                console.log('JS Function: eval', jsCode);
                const result = eval(jsCode);
                if (result && typeof result.then === 'function') {
                    return result;
                }
                return Promise.resolve(result);
            } catch (e) {
                console.error('HTMX JS Function error:', e);
                return Promise.reject(e);
            }
        }
        
        // Fall back to original ajax for non-javascript URLs
        return originalAjax.call(this, verb, path, params);
    };
}

// Helper function to make HTMX calls from JavaScript
window.HTMX = {
    /**
     * Make an HTMX request to a JavaScript function
     */
    call: function(funcPath, options = {}) {
        const verb = options.verb || 'GET';
        const target = options.target || '#content';
        const swap = options.swap || 'innerHTML';
        
        return htmx.ajax(verb, `javascript:${funcPath}`, {
            target: target,
            swap: swap
        });
    },
    
    /**
     * Trigger a refresh on an element
     */
    refresh: function(selector) {
        const el = document.querySelector(selector);
        if (el) {
            htmx.trigger(el, 'refresh');
        }
    },
    
    /**
     * Find element and swap content
     */
    swap: function(selector, content) {
        const el = document.querySelector(selector);
        if (el) {
            el.innerHTML = content;
        }
    }
};

// Log HTMX events for debugging
if (typeof htmx !== 'undefined') {
    document.body.addEventListener('htmx:configRequest', function(e) {
        console.log('HTMX config request:', e.detail);
    });
    
    document.body.addEventListener('htmx:afterRequest', function(e) {
        console.log('HTMX after request:', e.detail);
    });
    
    document.body.addEventListener('htmx:responseError', function(e) {
        console.error('HTMX response error:', e.detail);
    });
}
