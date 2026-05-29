(() => {
    const isAdminPage = () => Boolean(document.querySelector('.admin-page'));

    const ensureFlash = () => {
        const container = document.querySelector('.admin-container');
        if (!container) {
            return null;
        }

        let flash = container.querySelector('.admin-dynamic-flash');
        if (flash) {
            return flash;
        }

        flash = document.createElement('div');
        flash.className = 'admin-dynamic-flash';

        const anchor = container.querySelector('.admin-stats') || container.firstElementChild;
        container.insertBefore(flash, anchor ? anchor.nextElementSibling : null);

        return flash;
    };

    const showFlash = (message, isSuccess) => {
        const flash = ensureFlash();
        if (!flash || !message) {
            return;
        }

        flash.textContent = message;
        flash.className = `admin-dynamic-flash ${isSuccess ? 'success' : 'error'} visible`;
    };

    const markCardProcessed = (card, data) => {
        if (!card) {
            return;
        }

        if (data.removeCard) {
            card.classList.add('is-removing');
            window.setTimeout(() => card.remove(), 240);
            return;
        }

        const status = card.querySelector('.request-status');
        if (status && data.status) {
            status.textContent = data.status;
            status.className = `request-status ${data.statusClass || ''}`.trim();
        }

        if (data.disableActions !== false) {
            card.querySelectorAll('.request-actions form').forEach((form) => form.remove());
        }

        card.classList.add('is-processed');
    };

    document.addEventListener('submit', async (event) => {
        const form = event.target.closest('form[data-admin-dynamic-form]');
        if (!form || !window.fetch || !isAdminPage()) {
            return;
        }

        event.preventDefault();

        const confirmation = form.dataset.confirm;
        if (confirmation && !window.confirm(confirmation)) {
            return;
        }

        const button = form.querySelector('button[type="submit"], button:not([type])');
        const card = form.closest('.request-card');

        form.classList.add('is-submitting');
        card?.classList.add('is-busy');

        if (button) {
            button.disabled = true;
        }

        try {
            const response = await fetch(form.action || window.location.href, {
                method: form.method || 'POST',
                body: new FormData(form),
                credentials: 'same-origin',
                headers: {
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const contentType = response.headers.get('content-type') || '';
            if (!contentType.includes('application/json')) {
                window.location.reload();
                return;
            }

            const data = await response.json();
            showFlash(data.message, response.ok && data.ok !== false);

            if ((response.ok && data.ok !== false) || data.status || data.removeCard) {
                markCardProcessed(card, data);
            }
        } catch {
            showFlash('Не удалось выполнить действие. Проверь соединение и попробуй снова.', false);
        } finally {
            form.classList.remove('is-submitting');
            card?.classList.remove('is-busy');

            if (button) {
                button.disabled = false;
            }
        }
    });
})();
