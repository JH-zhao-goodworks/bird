using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class ReservationForm : Form
{
    private readonly BirdRepository _birdRepository;
    private readonly CageRepository _cageRepository;
    private readonly ReservationRepository _reservationRepository;
    private readonly OwnerRepository _ownerRepository;

    private List<Bird> _birds = new();
    private List<Cage> _cages = new();
    private List<Owner> _owners = new();

    private ComboBox _ownerFilterCombo = null!;
    private CheckedListBox _birdCheckList = null!;
    private DateTimePicker _startDatePicker = null!;
    private CheckBox _indefiniteCheck = null!;
    private DateTimePicker _endDatePicker = null!;
    private CheckBox _overrideCapacityCheck = null!;
    private TextBox _notesBox = null!;
    private DataGridView _availabilityGrid = null!;
    private List<(Cage Cage, int Occupied, int Remaining)> _availabilityRows = new();
    private Button _registerButton = null!;

    private DataGridView _reservationGrid = null!;
    private List<Reservation> _reservations = new();

    public ReservationForm(BirdRepository birdRepository, CageRepository cageRepository, ReservationRepository reservationRepository, OwnerRepository ownerRepository)
    {
        _birdRepository = birdRepository;
        _cageRepository = cageRepository;
        _reservationRepository = reservationRepository;
        _ownerRepository = ownerRepository;

        BuildUi();
        LoadLookups();
        RefreshReservationGrid();
    }

    private void BuildUi()
    {
        Text = "予約登録・空き籠確認";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 480, Orientation = Orientation.Vertical };

        // ---- 左側: 新規予約 ----
        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5,
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 190)); // 鳥の選択
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 期間など
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 空き籠確認ボタン
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 空き籠一覧
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 登録ボタン

        // 鳥の選択（飼い主で絞り込み + 複数選択可能なチェックリスト）
        var birdSelectionPanel = new Panel { Dock = DockStyle.Fill };
        var ownerFilterRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, AutoSize = false };
        ownerFilterRow.Controls.Add(new Label { Text = "飼い主で絞り込み", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 4, 0) });
        _ownerFilterCombo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        _ownerFilterCombo.SelectedIndexChanged += (_, _) => RefreshBirdCheckList();
        ownerFilterRow.Controls.Add(_ownerFilterCombo);

        _birdCheckList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _birdCheckList.ItemCheck += OnBirdItemCheck;

        birdSelectionPanel.Controls.Add(_birdCheckList);
        birdSelectionPanel.Controls.Add(ownerFilterRow);
        birdSelectionPanel.Controls.Add(new Label { Text = "鳥（複数選択可・同じ籠にまとめて登録できます）", Dock = DockStyle.Top, AutoSize = true, Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold) });

        var form = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, AutoSize = true };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        form.Controls.Add(new Label { Text = "開始日", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        _startDatePicker = new DateTimePicker { Width = 150, Format = DateTimePickerFormat.Short, Value = DateTime.Today };
        form.Controls.Add(_startDatePicker, 1, 0);

        form.Controls.Add(new Label { Text = "終了日", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 1);
        var endPanel = new FlowLayoutPanel { AutoSize = true };
        _endDatePicker = new DateTimePicker { Width = 150, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(1) };
        _indefiniteCheck = new CheckBox { Text = "期間なし（経営者の鳥など）", AutoSize = true, Margin = new Padding(10, 4, 0, 0) };
        _indefiniteCheck.CheckedChanged += (_, _) => _endDatePicker.Enabled = !_indefiniteCheck.Checked;
        endPanel.Controls.Add(_endDatePicker);
        endPanel.Controls.Add(_indefiniteCheck);
        form.Controls.Add(endPanel, 1, 1);

        form.Controls.Add(new Label { Text = "備考", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 2);
        _notesBox = new TextBox { Width = 320 };
        form.Controls.Add(_notesBox, 1, 2);

        form.Controls.Add(new Label(), 0, 3);
        _overrideCapacityCheck = new CheckBox { Text = "定員を超えても登録する（特別対応）", AutoSize = true };
        form.Controls.Add(_overrideCapacityCheck, 1, 3);

        var searchButton = new Button { Text = "空き籠を確認", Width = 140, Height = 34, Anchor = AnchorStyles.Left, Margin = new Padding(90, 6, 0, 10) };
        searchButton.Click += (_, _) => SearchAvailability();

        _availabilityGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowTemplate = { Height = 30 },
        };
        _availabilityGrid.Columns.Add("Name", "籠");
        _availabilityGrid.Columns.Add("Capacity", "定員");
        _availabilityGrid.Columns.Add("Occupied", "予定在籠数");
        _availabilityGrid.Columns.Add("Remaining", "空き");
        _availabilityGrid.Columns.Add("Priority", "優先");
        foreach (DataGridViewColumn col in _availabilityGrid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable; // 行の並びと_availabilityRowsの添字を一致させ続けるため

        _registerButton = new Button { Text = "選択した籠に登録", Height = 36, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        _registerButton.Click += (_, _) => RegisterReservation();

        leftPanel.Controls.Add(birdSelectionPanel, 0, 0);
        leftPanel.Controls.Add(form, 0, 1);
        leftPanel.Controls.Add(searchButton, 0, 2);
        leftPanel.Controls.Add(_availabilityGrid, 0, 3);
        leftPanel.Controls.Add(_registerButton, 0, 4);

        split.Panel1.Controls.Add(leftPanel);

        // ---- 右側: 予約一覧 ----
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var rightLabel = new Label { Text = "予約一覧", Dock = DockStyle.Top, Height = 24, Font = new Font("Yu Gothic UI", 11F, FontStyle.Bold) };
        var reservationButtonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight };
        var editReservationButton = new Button { Text = "編集", Width = 100, Height = 32 };
        editReservationButton.Click += (_, _) => EditSelectedReservation();
        var deleteButton = new Button { Text = "選択した予約を取消", Width = 150, Height = 32 };
        deleteButton.Click += (_, _) => DeleteSelectedReservation();
        reservationButtonPanel.Controls.Add(editReservationButton);
        reservationButtonPanel.Controls.Add(deleteButton);

        _reservationGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowTemplate = { Height = 30 },
        };
        _reservationGrid.Columns.Add("Bird", "鳥");
        _reservationGrid.Columns.Add("Cage", "籠");
        _reservationGrid.Columns.Add("Start", "開始日");
        _reservationGrid.Columns.Add("End", "終了日");
        _reservationGrid.Columns.Add("Notes", "備考");
        foreach (DataGridViewColumn col in _reservationGrid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable; // 行の並びと_reservationsの添字を一致させ続けるため
        _reservationGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelectedReservation(); };

        rightPanel.Controls.Add(_reservationGrid);
        rightPanel.Controls.Add(reservationButtonPanel);
        rightPanel.Controls.Add(rightLabel);
        rightPanel.Controls.SetChildIndex(_reservationGrid, 0);
        rightPanel.Controls.SetChildIndex(reservationButtonPanel, 1);
        rightPanel.Controls.SetChildIndex(rightLabel, 2);

        split.Panel2.Controls.Add(rightPanel);

        Controls.Add(split);
    }

    private void LoadLookups()
    {
        _owners = _ownerRepository.GetAll();
        _ownerFilterCombo.Items.Clear();
        _ownerFilterCombo.Items.Add("（すべて）");
        foreach (var owner in _owners)
            _ownerFilterCombo.Items.Add(owner);
        _ownerFilterCombo.SelectedIndex = 0;

        _birds = _birdRepository.GetAll();
        _cages = _cageRepository.GetAll();
        RefreshBirdCheckList();
    }

    private void RefreshBirdCheckList()
    {
        _birdCheckList.Items.Clear();
        var filterOwner = _ownerFilterCombo.SelectedItem as Owner;
        var birdsToShow = filterOwner is null ? _birds : _birds.Where(b => b.OwnerId == filterOwner.Id).ToList();
        foreach (var bird in birdsToShow)
            _birdCheckList.Items.Add(bird);
    }

    private void OnBirdItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (e.NewValue != CheckState.Checked) return;
        if (_birdCheckList.Items[e.Index] is not Bird bird) return;

        if (bird.IsProprietorBird)
        {
            _indefiniteCheck.Checked = true;
            _endDatePicker.Enabled = false;
        }
    }

    private List<Bird> GetCheckedBirds() => _birdCheckList.CheckedItems.Cast<Bird>().ToList();

    private void SearchAvailability()
    {
        var start = _startDatePicker.Value.Date;
        DateTime? end = _indefiniteCheck.Checked ? null : _endDatePicker.Value.Date;

        if (end is not null && end < start)
        {
            MessageBox.Show("終了日は開始日以降にしてください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 経営者の鳥は末尾が1・2の籠を優先し、末尾が5・6の籠は最後に回した順番で並べる
        var forProprietor = GetCheckedBirds().Any(b => b.IsProprietorBird);
        var orderedCages = _cages.OrderBy(c => c.AssignmentPriority(forProprietor)).ToList();

        _availabilityRows.Clear();
        _availabilityGrid.Rows.Clear();
        foreach (var cage in orderedCages)
        {
            var occupied = _reservationRepository.CountOverlapping(cage.Id, start, end);
            // 鳥が1羽でもいる期間は、定員に関わらず「空きなし」として扱う（同じ籠に別の予約の鳥を混在させないため）
            var remaining = occupied > 0 ? 0 : cage.Capacity;
            _availabilityRows.Add((cage, occupied, remaining));

            var note = cage.IsLastResort ? "空きが無い時のみ"
                : cage.IsProprietorPreferred ? "経営者の鳥を優先"
                : "";

            var rowIndex = _availabilityGrid.Rows.Add(cage.Name, cage.Capacity, occupied, remaining, note);
            if (remaining <= 0)
                _availabilityGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.MistyRose;
        }
    }

    private void RegisterReservation()
    {
        var selectedBirds = GetCheckedBirds();
        if (selectedBirds.Count == 0)
        {
            MessageBox.Show("鳥を1羽以上選択してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_availabilityGrid.SelectedRows.Count == 0)
        {
            MessageBox.Show("先に「空き籠を確認」を実行し、登録先の籠を選択してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var rowIndex = _availabilityGrid.SelectedRows[0].Index;
        var (cage, occupied, _) = _availabilityRows[rowIndex];

        var start = _startDatePicker.Value.Date;
        DateTime? end = _indefiniteCheck.Checked ? null : _endDatePicker.Value.Date;

        // 既に鳥がいる（＝別の予約が入っている）か、今回まとめて入れる羽数が定員を超える場合は特別対応が必要
        var wouldBeTotal = occupied + selectedBirds.Count;
        if ((occupied > 0 || wouldBeTotal > cage.Capacity) && !_overrideCapacityCheck.Checked)
        {
            MessageBox.Show(
                $"「{cage.Name}」は選択期間中に既に鳥がいるか、定員（{cage.Capacity}羽）を超えます。\n特別対応で登録する場合は「定員を超えても登録する」にチェックしてください。",
                "登録できません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        foreach (var bird in selectedBirds)
        {
            _reservationRepository.Insert(new Reservation
            {
                BirdId = bird.Id,
                CageId = cage.Id,
                StartDate = start,
                EndDate = end,
                Notes = _notesBox.Text.Trim(),
            });
        }

        for (int i = 0; i < _birdCheckList.Items.Count; i++)
            _birdCheckList.SetItemChecked(i, false);

        var names = string.Join("、", selectedBirds.Select(b => b.Name));
        MessageBox.Show($"「{names}」を「{cage.Name}」に登録しました。", "登録完了", MessageBoxButtons.OK, MessageBoxIcon.Information);

        SearchAvailability();
        RefreshReservationGrid();
    }

    private void RefreshReservationGrid()
    {
        _reservations = _reservationRepository.GetAll();
        _reservationGrid.Rows.Clear();
        foreach (var r in _reservations)
        {
            _reservationGrid.Rows.Add(
                r.BirdName,
                r.CageName,
                r.StartDate.ToString("yyyy-MM-dd"),
                r.IsIndefinite ? "期間なし" : r.EndDate!.Value.ToString("yyyy-MM-dd"),
                r.Notes);
        }
    }

    private void EditSelectedReservation()
    {
        var reservation = GetSelectedReservation();
        if (reservation is null) return;

        using var editForm = new ReservationEditForm(reservation, _cageRepository, _reservationRepository);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _reservationRepository.Update(editForm.EditedReservation);
            RefreshReservationGrid();
            SearchAvailability();
        }
    }

    private Reservation? GetSelectedReservation()
    {
        if (_reservationGrid.SelectedRows.Count == 0) return null;
        var index = _reservationGrid.SelectedRows[0].Index;
        return index >= 0 && index < _reservations.Count ? _reservations[index] : null;
    }

    private void DeleteSelectedReservation()
    {
        var reservation = GetSelectedReservation();
        if (reservation is null) return;

        var confirm = MessageBox.Show($"「{reservation.BirdName}」の予約（{reservation.CageName}）を取り消しますか？", "確認",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            _reservationRepository.Delete(reservation.Id);
            RefreshReservationGrid();
            SearchAvailability();
        }
    }
}
