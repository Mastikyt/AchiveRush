(() => {
    const storageKey = 'achiverush-theme';
    const root = document.documentElement;

    const normalizeTheme = (theme) => theme === 'light' || theme === 'dark' ? theme : null;

    const readSavedTheme = () => {
        try {
            return normalizeTheme(localStorage.getItem(storageKey));
        } catch {
            return null;
        }
    };

    const saveTheme = (theme) => {
        try {
            localStorage.setItem(storageKey, theme);
        } catch {
        }
    };

    const getSystemTheme = () => window.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
    const resolveTheme = (preferredTheme = readSavedTheme()) => normalizeTheme(preferredTheme) || getSystemTheme();

    const setButtonState = (button, theme) => {
        const isLight = theme === 'light';
        const icon = button.querySelector('[data-theme-toggle-icon]');
        const text = button.querySelector('[data-theme-toggle-text]');

        button.setAttribute('aria-pressed', isLight ? 'true' : 'false');
        button.setAttribute('title', isLight ? 'Включить темную тему' : 'Включить светлую тему');
        button.classList.toggle('is-light', isLight);
        button.classList.toggle('is-dark', !isLight);

        if (icon) {
            icon.textContent = isLight ? '☀' : '☾';
        }

        if (text) {
            text.textContent = isLight ? 'Светлая' : 'Темная';
        }
    };

    const applyTheme = (preferredTheme = readSavedTheme()) => {
        const theme = resolveTheme(preferredTheme);

        root.dataset.theme = theme;
        root.dataset.themeSource = normalizeTheme(preferredTheme) ? 'manual' : 'system';

        document.querySelectorAll('[data-theme-toggle]').forEach((button) => {
            setButtonState(button, theme);
        });
    };

    const bindSystemThemeListener = () => {
        const mediaQuery = window.matchMedia?.('(prefers-color-scheme: light)');
        if (!mediaQuery) {
            return;
        }

        const refreshIfSystemMode = () => {
            if (!readSavedTheme()) {
                applyTheme(null);
            }
        };

        if (typeof mediaQuery.addEventListener === 'function') {
            mediaQuery.addEventListener('change', refreshIfSystemMode);
        } else if (typeof mediaQuery.addListener === 'function') {
            mediaQuery.addListener(refreshIfSystemMode);
        }
    };

    document.addEventListener('DOMContentLoaded', () => {
        applyTheme(readSavedTheme());

        document.querySelectorAll('[data-theme-toggle]').forEach((button) => {
            button.addEventListener('click', () => {
                const currentPreference = readSavedTheme() || getSystemTheme();
                const nextPreference = currentPreference === 'light' ? 'dark' : 'light';

                saveTheme(nextPreference);
                applyTheme(nextPreference);
            });
        });

        bindSystemThemeListener();
    });
})();
