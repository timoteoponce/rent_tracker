// RentTracker JavaScript - Minimal vanilla JS
// No frameworks, simple and maintainable

(function() {
    'use strict';

    // Initialize on page load
    document.addEventListener('DOMContentLoaded', function() {
        initConfirmDialogs();
        initMobileMenu();
        initThemeToggle();
        initActiveNav();
        initToasts();
        registerServiceWorker();
    });

    // Confirm dialogs for destructive actions
    function initConfirmDialogs() {
        var confirmButtons = document.querySelectorAll('[data-confirm]');
        confirmButtons.forEach(function(button) {
            button.addEventListener('click', function(e) {
                var message = button.getAttribute('data-confirm') || 'Are you sure?';
                if (!confirm(message)) {
                    e.preventDefault();
                }
            });
        });
    }

    // Mobile navigation hamburger menu
    function initMobileMenu() {
        var toggle = document.getElementById('nav-toggle');
        var header = document.querySelector('.app-header');
        if (!toggle || !header) {
            return;
        }

        toggle.addEventListener('click', function() {
            var isOpen = header.classList.toggle('menu-open');
            toggle.setAttribute('aria-expanded', isOpen);
        });

        // Close menu when clicking a nav link on mobile
        var navLinks = header.querySelectorAll('.header-nav a');
        navLinks.forEach(function(link) {
            link.addEventListener('click', function() {
                header.classList.remove('menu-open');
                toggle.setAttribute('aria-expanded', 'false');
            });
        });

        // Close menu on Escape key and return focus to toggle
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' && header.classList.contains('menu-open')) {
                header.classList.remove('menu-open');
                toggle.setAttribute('aria-expanded', 'false');
                toggle.focus();
            }
        });
    }

    // Dark mode toggle
    function initThemeToggle() {
        // Listen for system-level theme changes in real time.
        // Only apply them if the user has never clicked the manual toggle.
        var mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        mediaQuery.addEventListener('change', function(e) {
            var storedTheme = localStorage.getItem('theme');
            if (storedTheme !== null) {
                return; // User has an explicit preference; do not override it
            }

            var html = document.documentElement;
            var meta = document.getElementById('theme-color-meta');
            if (e.matches) {
                html.setAttribute('data-theme', 'dark');
                if (meta) { meta.content = '#1e3a8a'; }
            } else {
                html.removeAttribute('data-theme');
                if (meta) { meta.content = '#2563eb'; }
            }

            updateChartColors();
        });

        // Manual toggle button — stores an explicit preference
        var toggles = document.querySelectorAll('#theme-toggle');
        toggles.forEach(function(toggle) {
            toggle.addEventListener('click', function() {
                var html = document.documentElement;
                var meta = document.getElementById('theme-color-meta');
                var isDark = html.getAttribute('data-theme') === 'dark';

                if (isDark) {
                    html.removeAttribute('data-theme');
                    localStorage.setItem('theme', 'light');
                    if (meta) {
                        meta.content = '#2563eb';
                    }
                } else {
                    html.setAttribute('data-theme', 'dark');
                    localStorage.setItem('theme', 'dark');
                    if (meta) {
                        meta.content = '#1e3a8a';
                    }
                }

                // Notify charts to update colors if any are present
                updateChartColors();
            });
        });
    }

    // Active navigation highlighting based on current URL
    function initActiveNav() {
        var currentPath = window.location.pathname;
        var navLinks = document.querySelectorAll('.header-nav a');

        navLinks.forEach(function(link) {
            var href = link.getAttribute('href');
            if (!href) {
                return;
            }

            // Normalize href to path (remove leading ~ or .)
            var linkPath = href.replace(/^~/, '');
            if (linkPath === './') {
                linkPath = '/';
            }

            // Exact match for home, starts-with for others
            var isActive = false;
            if (linkPath === '/') {
                isActive = currentPath === '/';
            } else {
                isActive = currentPath.indexOf(linkPath) === 0;
            }

            if (isActive) {
                link.classList.add('active');
            } else {
                link.classList.remove('active');
            }
        });
    }

    // Toast notifications: auto-dismiss and close buttons
    function initToasts() {
        var toasts = document.querySelectorAll('[data-toast]');
        toasts.forEach(function(toast) {
            // Auto-dismiss after 5 seconds
            var timer = setTimeout(function() {
                dismissToast(toast);
            }, 5000);

            // Manual close button
            var closeBtn = toast.querySelector('.toast-close');
            if (closeBtn) {
                closeBtn.addEventListener('click', function() {
                    clearTimeout(timer);
                    dismissToast(toast);
                });
            }
        });
    }

    // Global showToast function for dynamic notifications
    window.showToast = function(message, type) {
        type = type || 'info';
        var container = document.getElementById('toast-container');

        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'toast-container';
            container.setAttribute('aria-live', 'polite');
            container.setAttribute('aria-label', 'Notifications');
            document.body.appendChild(container);
        }

        var toast = document.createElement('div');
        toast.className = 'toast toast-' + type;
        toast.setAttribute('data-toast', '');
        toast.setAttribute('role', type === 'error' || type === 'warning' ? 'alert' : 'status');
        toast.innerHTML = '<span>' + message + '</span>' +
            '<button type="button" class="toast-close" aria-label="Close notification">&times;</button>';

        var closeBtn = toast.querySelector('.toast-close');
        if (closeBtn) {
            closeBtn.addEventListener('click', function() {
                dismissToast(toast);
            });
        }

        container.appendChild(toast);

        // Auto-dismiss
        setTimeout(function() {
            dismissToast(toast);
        }, 5000);
    };

    function dismissToast(toast) {
        if (!toast || toast.classList.contains('toast-hiding')) {
            return;
        }
        toast.classList.add('toast-hiding');
        toast.addEventListener('animationend', function() {
            toast.remove();
        });
    }

    // Notify any Chart.js instances to refresh colors after theme change
    window.updateChartColors = function() {
        if (typeof Chart === 'undefined') {
            return;
        }

        var isDark = document.documentElement.getAttribute('data-theme') === 'dark';
        var textColor = isDark ? '#9ca3af' : '#6b7280';
        var gridColor = isDark ? '#374151' : '#e5e7eb';

        Chart.defaults.color = textColor;
        Chart.defaults.borderColor = gridColor;

        // Trigger update on all existing charts
        Chart.instances.forEach(function(chart) {
            if (chart && typeof chart.update === 'function') {
                // Update dataset colors if they were defined with hardcoded values
                // Pages that want full color swap should handle this in their own scripts
                chart.update();
            }
        });
    };

    // Service Worker registration with update banner support
    function registerServiceWorker() {
        if (!('serviceWorker' in navigator)) {
            return;
        }

        navigator.serviceWorker.register('/js/sw.js').then(function(reg) {
            // Check for updates when the page loads
            reg.addEventListener('updatefound', function() {
                var newWorker = reg.installing;
                newWorker.addEventListener('statechange', function() {
                    if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                        // New version available — show refresh banner
                        showUpdateBanner();
                    }
                });
            });

            // Also check periodically (every 60 minutes) for updates
            setInterval(function() {
                reg.update();
            }, 60 * 60 * 1000);
        });

        // Listen for messages from the service worker
        navigator.serviceWorker.addEventListener('message', function(event) {
            if (event.data && event.data.type === 'UPDATE_CHECKED') {
                // Optional: update UI based on check result
            }
        });
    }

    // Show a non-intrusive banner prompting the user to refresh
    function showUpdateBanner() {
        var banner = document.createElement('div');
        banner.id = 'sw-update-banner';
        banner.innerHTML =
            '<span>An update is available. Refresh to get the latest version.</span>' +
            '<button onclick="window.location.reload()">Refresh</button>';
        document.body.appendChild(banner);
    }
})();
