using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class CageMoveForm : Form
{
    private readonly Cage _sourceCage;
    private readonly CageRepository _cageRepository;
    private readonly ReservationRepository _reservationRepository;

    private List<Cage> _cages = new();
    private List<Reservation> _sourceReservations = new();
    private List<Reservation> _destinationReservations = new();

    private CheckedListBox _birdCheckList = null!;
    private ComboBox _destinationCageCombo = null!;
    private CheckedListBox _swapPartnerCheckList = null!;
    private Label _statusLabel = null!;

    public CageMoveForm(Cage sourceCage, CageRepository cageRepository, ReservationRepository reservationRepository)
    {
        _sourceCage = sourceCage;
        _cageRepository = cageRepository;
        _reservationRepository = reservationRepository;

        BuildUi();
        LoadLookups();
    }

    private void BuildUi()
    {
        Text = $"「{_sourceCage.Name}」の鳥を移動・交換";
        Width = 560;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(16) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 移動する鳥ラベル＋補助ボタン
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // 移動する鳥リスト
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 移動先の籠
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 交換相手ラベル
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // 交換相手リスト
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 状態

        var birdHeaderPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        birdHeaderPanel.Controls.Add(new Label
        {
            Text = "移動する鳥（複数選択可・ペアはまとめて選択）",
            AutoSize = true,
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 6, 8, 0),
        });
        var selectSamePeriodButton = new Button { Text = "同じ期間の鳥もまとめて選択", Width = 200, Height = 26 };
        selectSamePeriodButton.Click += (_, _) => SelectSamePeriodBirds();
        birdHeaderPanel.Controls.Add(selectSamePeriodButton);
        root.Controls.Add(birdHeaderPanel, 0, 0);

        _birdCheckList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _birdCheckList.ItemCheck += (_, _) => BeginInvoke(RefreshStatus);
        root.Controls.Add(_birdCheckList, 0, 1);

        var destinationPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        destinationPanel.Controls.Add(new Label { Text = "移動先の籠", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        _destinationCageCombo = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
        _destinationCageCombo.SelectedIndexChanged += (_, _) => OnDestinationChanged();
        destinationPanel.Controls.Add(_destinationCageCombo);
        root.Controls.Add(destinationPanel, 0, 2);

        root.Controls.Add(new Label
        {
            Text = "交換相手（移動先の鳥・複数選択可。選ばなければそのまま移動）",
            Dock = DockStyle.Top,
            AutoSize = true,
            Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 0),
        }, 0, 3);

        _swapPartnerCheckList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _swapPartnerCheckList.ItemCheck += (_, _) => BeginInvoke(RefreshStatus);
        root.Controls.Add(_swapPartnerCheckList, 0, 4);

        _statusLabel = new Label { Dock = DockStyle.Top, AutoSize = true, MaximumSize = new Size(490, 0), Margin = new Padding(0, 8, 0, 0) };
        root.Controls.Add(_statusLabel, 0, 5);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50, Padding = new Padding(10) };
        var cancelButton = new Button { Text = "閉じる", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var executeButton = new Button { Text = "実行", Width = 90, Height = 32 };
        executeButton.Click += (_, _) => TryExecute();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(executeButton);
        AcceptButton = executeButton;
        CancelButton = cancelButton;

        Controls.Add(root);
        Controls.Add(buttonPanel);
    }

    private void LoadLookups()
    {
        _cages = _cageRepository.GetAll();

        _sourceReservations = _reservationRepository.GetByCage(_sourceCage.Id);
        _birdCheckList.Items.Clear();
        foreach (var reservation in _sourceReservations)
            _birdCheckList.Items.Add(new ReservationItem(reservation));

        _destinationCageCombo.Items.Clear();
        foreach (var cage in _cages.Where(c => c.Id != _sourceCage.Id))
            _destinationCageCombo.Items.Add(cage);
        if (_destinationCageCombo.Items.Count > 0)
            _destinationCageCombo.SelectedIndex = 0;
    }

    private record ReservationItem(Reservation Reservation)
    {
        public override string ToString()
        {
            var period = Reservation.IsIndefinite
                ? "無期限"
                : $"{Reservation.StartDate:yyyy/MM/dd}〜{Reservation.EndDate!.Value:yyyy/MM/dd}";
            return $"{Reservation.BirdName}（{period}）";
        }
    }

    // チェック済みの鳥と同じ期間の鳥をまとめてチェックする（ペアで預かっている鳥を一度に選ぶため）
    private void SelectSamePeriodBirds()
    {
        var checkedReservations = GetCheckedReservations(_birdCheckList);
        if (checkedReservations.Count == 0)
        {
            MessageBox.Show("先に基準となる鳥を1羽チェックしてください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        for (var i = 0; i < _birdCheckList.Items.Count; i++)
        {
            if (_birdCheckList.Items[i] is not ReservationItem item) continue;
            var samePeriod = checkedReservations.Any(c =>
                c.StartDate == item.Reservation.StartDate && c.EndDate == item.Reservation.EndDate);
            if (samePeriod)
                _birdCheckList.SetItemChecked(i, true);
        }

        RefreshStatus();
    }

    private void OnDestinationChanged()
    {
        _swapPartnerCheckList.Items.Clear();

        if (_destinationCageCombo.SelectedItem is Cage destination)
        {
            _destinationReservations = _reservationRepository.GetByCage(destination.Id);
            foreach (var reservation in _destinationReservations)
                _swapPartnerCheckList.Items.Add(new ReservationItem(reservation));
        }
        else
        {
            _destinationReservations = new List<Reservation>();
        }

        RefreshStatus();
    }

    private static List<Reservation> GetCheckedReservations(CheckedListBox list) =>
        list.CheckedItems.Cast<ReservationItem>().Select(i => i.Reservation).ToList();

    // 無期限の予約は重複として数えない（経営者の鳥などが常駐していても移動・交換できるようにするため）
    private static bool Conflicts(Reservation a, Reservation b)
    {
        if (a.IsIndefinite || b.IsIndefinite) return false;
        return a.StartDate <= b.EndDate!.Value && b.StartDate <= a.EndDate!.Value;
    }

    private void RefreshStatus()
    {
        var check = ValidateMove();
        _statusLabel.Text = check.Message;
        _statusLabel.ForeColor = check.CanExecute ? Color.DarkGreen : Color.Firebrick;
    }

    private (bool CanExecute, string Message, List<Reservation> Moving, Cage? Destination, List<Reservation> Partners) ValidateMove()
    {
        var moving = GetCheckedReservations(_birdCheckList);
        var partners = GetCheckedReservations(_swapPartnerCheckList);
        var empty = new List<Reservation>();

        if (moving.Count == 0)
            return (false, "移動する鳥をチェックしてください。", empty, null, empty);
        if (_destinationCageCombo.SelectedItem is not Cage destination)
            return (false, "移動先の籠がありません。", empty, null, empty);

        // 移動先に残る鳥（交換で出ていく鳥は除く）と期間が重ならないか確認する
        var remainingDestination = _destinationReservations.Where(r => partners.All(p => p.Id != r.Id)).ToList();
        foreach (var bird in moving)
        {
            var blocking = remainingDestination.FirstOrDefault(r => Conflicts(r, bird));
            if (blocking is not null)
                return (false, $"実行できません。「{destination.Name}」の「{blocking.BirdName}」と「{bird.BirdName}」の期間が重なっています。", empty, null, empty);
        }

        // 元の籠に残る鳥（今回移動する鳥は除く）と、交換で入ってくる鳥の期間が重ならないか確認する
        var remainingSource = _sourceReservations.Where(r => moving.All(m => m.Id != r.Id)).ToList();
        foreach (var partner in partners)
        {
            var blocking = remainingSource.FirstOrDefault(r => Conflicts(r, partner));
            if (blocking is not null)
                return (false, $"実行できません。「{_sourceCage.Name}」の「{blocking.BirdName}」と「{partner.BirdName}」の期間が重なっています。", empty, null, empty);
        }

        var movingNames = string.Join("、", moving.Select(r => r.BirdName));
        var message = partners.Count == 0
            ? $"「{movingNames}」を「{destination.Name}」へ移動できます。"
            : $"「{movingNames}」と「{string.Join("、", partners.Select(r => r.BirdName))}」を交換できます。";
        return (true, message, moving, destination, partners);
    }

    private void TryExecute()
    {
        var (canExecute, message, moving, destination, partners) = ValidateMove();
        if (!canExecute || destination is null)
        {
            MessageBox.Show(message, "実行できません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        foreach (var bird in moving)
        {
            bird.CageId = destination.Id;
            _reservationRepository.Update(bird);
        }
        foreach (var partner in partners)
        {
            partner.CageId = _sourceCage.Id;
            _reservationRepository.Update(partner);
        }

        var movingNames = string.Join("、", moving.Select(r => r.BirdName));
        var completedMessage = partners.Count == 0
            ? $"「{movingNames}」を「{destination.Name}」へ移動しました。"
            : $"「{movingNames}」と「{string.Join("、", partners.Select(r => r.BirdName))}」を交換しました。";
        MessageBox.Show(completedMessage, "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);

        DialogResult = DialogResult.OK;
        Close();
    }
}
