// RentTracker JavaScript - Minimal vanilla JS
// No frameworks, simple and maintainable

(function() {
    'use strict';

    // Initialize on page load
    document.addEventListener('DOMContentLoaded', function() {
        initConfirmDialogs();
        initMobileMenu();
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

    // Mobile menu toggle
    function initMobileMenu() {
        const menuToggle = document.getElementById('menu-toggle');
        const headerNav = document.querySelector('.header-nav');
        
        if (menuToggle && headerNav) {
            menuToggle.addEventListener('click', function() {
                headerNav.classList.toggle('mobile-open');
            });
        }
    }

    // Format currency for display
    window.formatCurrency = function(amount, currency) {
        if (currency === 'BOB') {
            return 'Bs. ' + parseFloat(amount).toFixed(2);
        }
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: currency || 'USD'
        }).format(amount);
    };

    // Format date for display
    window.formatDate = function(dateString) {
        const date = new Date(dateString);
        return date.toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric'
        });
    };
})();
