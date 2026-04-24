document.addEventListener('DOMContentLoaded', () => {
    // ===== МОДАЛКА =====
    const addGameModal = document.getElementById('addGameModal');
    const openAddGameBtn = document.getElementById('openAddGame');
    const cancelModalBtn = document.getElementById('cancelModal');
    const cancelModalTopBtn = document.getElementById('cancelModalTop');
    const closeModalOverlay = document.getElementById('closeModalOverlay');
    const steamUrlInput = document.getElementById('steamUrl');

    function openModal() {
        if (!addGameModal) return;

        addGameModal.classList.remove('hidden');
        document.body.style.overflow = 'hidden';

        setTimeout(() => {
            steamUrlInput?.focus();
        }, 50);
    }

    function closeModal() {
        if (!addGameModal) return;

        addGameModal.classList.add('hidden');
        document.body.style.overflow = '';
    }

    if (openAddGameBtn) {
        openAddGameBtn.addEventListener('click', openModal);
    }

    if (cancelModalBtn) {
        cancelModalBtn.addEventListener('click', closeModal);
    }

    if (cancelModalTopBtn) {
        cancelModalTopBtn.addEventListener('click', closeModal);
    }

    if (closeModalOverlay) {
        closeModalOverlay.addEventListener('click', closeModal);
    }

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && addGameModal && !addGameModal.classList.contains('hidden')) {
            closeModal();
        }
    });

    // ===== ФИЛЬТРЫ =====
    const filterName = document.getElementById('filterName');
    const filterAchievements = document.getElementById('filterAchievements');
    const applyFiltersBtn = document.getElementById('applyFilters');
    const resetFiltersBtn = document.getElementById('resetFilters');

    const catalogItems = document.querySelectorAll('.catalog > *');

    function applyFilters() {
        const nameVal = (filterName?.value || '').toLowerCase().trim();
        const achVal = parseInt(filterAchievements?.value, 10) || 0;

        catalogItems.forEach((el) => {
            if (el.id === 'openAddGame') {
                el.style.display = '';
                return;
            }

            const card = el.querySelector('.game-card');

            if (!card) {
                el.style.display = 'none';
                return;
            }

            const gameName = (card.dataset.name || '').toLowerCase();
            const achievementsCount = parseInt(card.dataset.achievements, 10) || 0;

            const matchName = !nameVal || gameName.includes(nameVal);
            const matchAchievements = achievementsCount >= achVal;

            el.style.display = matchName && matchAchievements ? '' : 'none';
        });
    }

    function resetFilters() {
        if (filterName) filterName.value = '';
        if (filterAchievements) filterAchievements.value = '';

        catalogItems.forEach((el) => {
            el.style.display = '';
        });
    }

    if (applyFiltersBtn) {
        applyFiltersBtn.addEventListener('click', applyFilters);
    }

    if (resetFiltersBtn) {
        resetFiltersBtn.addEventListener('click', resetFilters);
    }

    if (filterName) {
        filterName.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                applyFilters();
            }
        });
    }

    if (filterAchievements) {
        filterAchievements.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                applyFilters();
            }
        });
    }
});