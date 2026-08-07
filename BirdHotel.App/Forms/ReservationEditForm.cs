using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class ReservationEditForm : Form
{
    private readonly ReservationRepository _reservationRepository;
    private List<Cage> _cages = new();

    public Reservation EditedReservation { get; }

    private ComboBox _cageCombo = null!;
    private DateTimePicker _startDatePicker = null!;
    private CheckBox _indefiniteCheck = null!;
    private DateTimePicker _endDatePicker = null!;
    private TextBox _notesBox = null!;
    private CheckBox _overrideCapacityCheck = null!;

    public ReservationEditForm(Reservation reservation, CageRepository cageRepository, ReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
        _cages = cageRepository.GetAll();

        EditedReservation = new Reservation
        {
            Id = reservation.Id,
            BirdId = reservation.BirdId,
            CageId = reservation.CageId,
            StartDate = reservation.StartDate,
            EndDate = reservation.EndDate,
            Notes = reservation.Notes,
            BirdName = reservation.BirdName,
            CageName = reservation.CageName,
        };

        BuildUi();
        LoadFromReservation();
    }

    private void BuildUi()
    {
        Text = $"予約の編集（{EditedReservation.BirdName}）";
        Width = 420;
        Height = 380;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Yu Gothic UI", 10F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "鳥", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        layout.Controls.Add(new Label { Text = EditedReservation.BirdName, AutoSize = true, Margin = new Padding(3, 8, 3, 3), Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold) }, 1, 0);

        layout.Controls.Add(new Label { Text = "籠", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 1);
        _cageCombo = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var cage in _cages)
            _cageCombo.Items.Add(cage);
        layout.Controls.Add(_cageCombo, 1, 1);

        layout.Controls.Add(new Label { Text = "開始日", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 2);
        _startDatePicker = new DateTimePicker { Width = 150, Format = DateTimePickerFormat.Short };
        layout.Controls.Add(_startDatePicker, 1, 2);

        layout.Controls.Add(new Label { Text = "終了日", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 3);
        var endPanel = new FlowLayoutPanel { AutoSize = true };
        _endDatePicker = new DateTimePicker { Width = 150, Format = DateTimePickerFormat.Short };
        _indefiniteCheck = new CheckBox { Text = "期間なし", AutoSize = true, Margin = new Padding(10, 4, 0, 0) };
        _indefiniteCheck.CheckedChanged += (_, _) => _endDatePicker.Enabled = !_indefiniteCheck.Checked;
        endPanel.Controls.Add(_endDatePicker);
        endPanel.Controls.Add(_indefiniteCheck);
        layout.Controls.Add(endPanel, 1, 3);

        layout.Controls.Add(new Label { Text = "備考", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 4);
        _notesBox = new TextBox { Width = 200 };
        layout.Controls.Add(_notesBox, 1, 4);

        layout.Controls.Add(new Label(), 0, 5);
        _overrideCapacityCheck = new CheckBox { Text = "定員を超えても登録する（特別対応）", AutoSize = true, MaximumSize = new Size(250, 0) };
        layout.Controls.Add(_overrideCapacityCheck, 1, 5);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50, Padding = new Padding(10) };
        var cancelButton = new Button { Text = "キャンセル", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var okButton = new Button { Text = "保存", Width = 90, Height = 32 };
        okButton.Click += (_, _) => TrySave();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(layout);
        Controls.Add(buttonPanel);
    }

    private void LoadFromReservation()
    {
        _cageCombo.SelectedItem = _cages.FirstOrDefault(c => c.Id == EditedReservation.CageId);
        _startDatePicker.Value = EditedReservation.StartDate;
        if (EditedReservation.EndDate is { } end)
        {
            _indefiniteCheck.Checked = false;
            _endDatePicker.Value = end;
        }
        else
        {
            _indefiniteCheck.Checked = true;
        }
        _notesBox.Text = EditedReservation.Notes;
    }

    private void TrySave()
    {
        if (_cageCombo.SelectedItem is not Cage selectedCage)
        {
            MessageBox.Show("籠を選択してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var start = _startDatePicker.Value.Date;
        DateTime? end = _indefiniteCheck.Checked ? null : _endDatePicker.Value.Date;

        if (end is not null && end < start)
        {
            MessageBox.Show("終了日は開始日以降にしてください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 自分自身の予約は除外して重複判定する（同じ予約を編集しても定員オーバー扱いにならないように）
        var occupied = _reservationRepository.CountOverlapping(selectedCage.Id, start, end, excludeReservationId: EditedReservation.Id);
        if (occupied > 0 && !_overrideCapacityCheck.Checked)
        {
            MessageBox.Show(
                $"「{selectedCage.Name}」は選択期間中に既に鳥がいます。\n特別対応で登録する場合は「定員を超えても登録する」にチェックしてください。",
                "登録できません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EditedReservation.CageId = selectedCage.Id;
        EditedReservation.StartDate = start;
        EditedReservation.EndDate = end;
        EditedReservation.Notes = _notesBox.Text.Trim();

        DialogResult = DialogResult.OK;
        Close();
    }
}
