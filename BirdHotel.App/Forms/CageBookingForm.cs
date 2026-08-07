using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class CageBookingForm : Form
{
    private readonly Cage _cage;
    private readonly BirdRepository _birdRepository;
    private readonly ReservationRepository _reservationRepository;
    private readonly OwnerRepository _ownerRepository;

    private List<Bird> _birds = new();
    private List<Owner> _owners = new();

    private Label _statusLabel = null!;
    private ComboBox _ownerFilterCombo = null!;
    private CheckedListBox _birdCheckList = null!;
    private DateTimePicker _startDatePicker = null!;
    private CheckBox _indefiniteCheck = null!;
    private DateTimePicker _endDatePicker = null!;
    private CheckBox _overrideCapacityCheck = null!;
    private TextBox _notesBox = null!;

    public CageBookingForm(Cage cage, BirdRepository birdRepository, ReservationRepository reservationRepository, OwnerRepository ownerRepository)
    {
        _cage = cage;
        _birdRepository = birdRepository;
        _reservationRepository = reservationRepository;
        _ownerRepository = ownerRepository;

        BuildUi();
        LoadLookups();
        RefreshStatus();
    }

    private void BuildUi()
    {
        Text = $"「{_cage.Name}」に鳥を登録";
        Width = 460;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Yu Gothic UI", 10F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 現況
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190)); // 鳥の選択
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 期間など
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // ボタン

        _statusLabel = new Label { AutoSize = true, Dock = DockStyle.Top, MaximumSize = new Size(420, 0), Margin = new Padding(0, 0, 0, 8) };
        root.Controls.Add(_statusLabel, 0, 0);

        var birdSelectionPanel = new Panel { Dock = DockStyle.Fill };
        var ownerFilterRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30 };
        ownerFilterRow.Controls.Add(new Label { Text = "飼い主で絞り込み", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 4, 0) });
        _ownerFilterCombo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        _ownerFilterCombo.SelectedIndexChanged += (_, _) => RefreshBirdCheckList();
        ownerFilterRow.Controls.Add(_ownerFilterCombo);

        _birdCheckList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _birdCheckList.ItemCheck += OnBirdItemCheck;

        birdSelectionPanel.Controls.Add(_birdCheckList);
        birdSelectionPanel.Controls.Add(ownerFilterRow);
        birdSelectionPanel.Controls.Add(new Label { Text = "登録する鳥（複数選択可）", Dock = DockStyle.Top, AutoSize = true, Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold) });
        root.Controls.Add(birdSelectionPanel, 0, 1);

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
        _notesBox = new TextBox { Width = 260 };
        form.Controls.Add(_notesBox, 1, 2);

        form.Controls.Add(new Label(), 0, 3);
        _overrideCapacityCheck = new CheckBox { Text = "定員を超えても登録する（特別対応）", AutoSize = true };
        form.Controls.Add(_overrideCapacityCheck, 1, 3);

        root.Controls.Add(form, 0, 2);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = new Button { Text = "キャンセル", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var registerButton = new Button { Text = "登録", Width = 90, Height = 32 };
        registerButton.Click += (_, _) => TryRegister();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(registerButton);
        AcceptButton = registerButton;
        CancelButton = cancelButton;
        root.Controls.Add(buttonPanel, 0, 3);

        Controls.Add(root);
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

    private void RefreshStatus()
    {
        var reservations = _reservationRepository.GetByCage(_cage.Id);
        var todayOccupants = reservations.Where(r => r.OverlapsWith(DateTime.Today, DateTime.Today)).ToList();
        var summary = todayOccupants.Count == 0
            ? "本日時点でこの籠は空いています。"
            : "本日時点の在籠: " + string.Join("、", todayOccupants.Select(r => r.BirdName));
        _statusLabel.Text = $"{_cage.Name}（定員{_cage.Capacity}羽）\n{summary}";
    }

    private List<Bird> GetCheckedBirds() => _birdCheckList.CheckedItems.Cast<Bird>().ToList();

    private void TryRegister()
    {
        var selectedBirds = GetCheckedBirds();
        if (selectedBirds.Count == 0)
        {
            MessageBox.Show("鳥を1羽以上選択してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var start = _startDatePicker.Value.Date;
        DateTime? end = _indefiniteCheck.Checked ? null : _endDatePicker.Value.Date;

        if (end is not null && end < start)
        {
            MessageBox.Show("終了日は開始日以降にしてください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 既に鳥がいる（＝別の予約が入っている）か、今回まとめて入れる羽数が定員を超える場合は特別対応が必要
        var occupied = _reservationRepository.CountOverlapping(_cage.Id, start, end);
        var wouldBeTotal = occupied + selectedBirds.Count;
        if ((occupied > 0 || wouldBeTotal > _cage.Capacity) && !_overrideCapacityCheck.Checked)
        {
            MessageBox.Show(
                $"「{_cage.Name}」は選択期間中に既に鳥がいるか、定員（{_cage.Capacity}羽）を超えます。\n特別対応で登録する場合は「定員を超えても登録する」にチェックしてください。",
                "登録できません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        foreach (var bird in selectedBirds)
        {
            _reservationRepository.Insert(new Reservation
            {
                BirdId = bird.Id,
                CageId = _cage.Id,
                StartDate = start,
                EndDate = end,
                Notes = _notesBox.Text.Trim(),
            });
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
