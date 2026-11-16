// "Assigned to me" toggle on a register: searches the "Assigned to" column (index in data-column) for the marker the
// server writes into the signed-in user's own rows. Call MintAssignedFilter(table) once the DataTable exists.
window.MintAssignedFilter = function (table) {
    var btn = document.getElementById('assigned-to-me-filter');
    if (!btn || !table) return;
    var col = parseInt(btn.getAttribute('data-column'), 10);
    var on = table.column(col).search() === '~assigned-to-me~';
    var show = function () { btn.classList.toggle('active', on); btn.setAttribute('aria-pressed', on ? 'true' : 'false'); };
    show();
    btn.addEventListener('click', function () {
        on = !on;
        table.column(col).search(on ? '~assigned-to-me~' : '', false, false).draw();
        show();
    });
};
