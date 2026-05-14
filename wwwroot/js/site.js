// RentTracker JavaScript - Minimal vanilla JS
// No frameworks, simple and maintainable

(function() {
    'use strict';

    // Initialize on page load
    document.addEventListener('DOMContentLoaded', function() {
        initConfirmDialogs();
        registerServiceWorker();
    });

    // Confirm dialogs for destructive actions
    function initConfirmDialogs() {
        const confirmButtons = document.querySelectorAll('[data-confirm]');
        confirmButtons.forEach(function(button) {
            button.addEventListener('click', function(e) {
                const message = button.getAttribute('data-confirm') || 'Are you sure?';
                if (!confirm(message)) {
                    e.preventDefault();
                }
            });
        });
    }

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
