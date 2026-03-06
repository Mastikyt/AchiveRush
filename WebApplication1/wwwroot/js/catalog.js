const addGameModal = document.getElementById('addGameModal');
const openAddGameBtn = document.getElementById('openAddGame');
const cancelModalBtn = document.getElementById('cancelModal');
const addGameForm = document.getElementById('addGameForm');

openAddGameBtn.addEventListener('click', () => addGameModal.classList.remove('hidden'));
cancelModalBtn.addEventListener('click', () => addGameModal.classList.add('hidden'));

addGameForm.addEventListener('submit', async (e) => {
    e.preventDefault();

    const steamUrl = document.getElementById('steamUrl').value;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    try {
        const res = await fetch('/Games/AddFromSteam', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `steamUrl=${encodeURIComponent(steamUrl)}&__RequestVerificationToken=${encodeURIComponent(token)}`
        });

        if (res.ok) {
            location.reload();
        } else {
            const text = await res.text();
            alert(`Ошибка: ${text}`);
        }
    } catch (err) {
        console.error(err);
        alert('Ошибка запроса');
    }
});

// Фильтры
const filterName = document.getElementById('filterName');
const filterAchievements = document.getElementById('filterAchievements');
const applyFiltersBtn = document.getElementById('applyFilters');
const resetFiltersBtn = document.getElementById('resetFilters');

applyFiltersBtn.addEventListener('click', () => {
    const nameVal = filterName.value.toLowerCase();
    const achVal = parseInt(filterAchievements.value) || 0;

    document.querySelectorAll('.game-card').forEach(card => {
        const cardName = card.dataset.name.toLowerCase();
        const cardAch = parseInt(card.dataset.achievements);

        if (cardName.includes(nameVal) && cardAch >= achVal) {
            card.style.display = '';
        } else {
            card.style.display = 'none';
        }
    });
});

resetFiltersBtn.addEventListener('click', () => {
    filterName.value = '';
    filterAchievements.value = '';
    document.querySelectorAll('.game-card').forEach(card => card.style.display = '');
});