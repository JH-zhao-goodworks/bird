using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class BirdMergeForm : Form
{
    private readonly BirdRepository _birdRepository;
    private readonly ReservationRepository _reservationRepository;
    private List<Bird> _birds = new();

    private CheckedListBox _birdCheckList = null!;
    private ComboBox _targetCombo = null!;

    public BirdMergeForm(BirdRepository birdRepository, ReservationRepository reservationRepository)
    {
        _birdRepository = birdRepository;
        _reservationRepository = reservationRepository;
        BuildUi();
        LoadBirds();
    }

    private void BuildUi()
    {
        Text = "鳥の重複統合";
        Width = 500;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var instructionLabel = new Label
        {
            Text = "同じ鳥なのに別々に登録されてしまった鳥をまとめます。\n" +
                   "同じ鳥だと思うものに2件以上チェックを付け、下で「残す鳥」を選んでください。\n" +
                   "チェックした他の鳥の予約はすべて「残す鳥」の予約に付け替えられ、重複していた鳥は削除されます（元に戻せません）。",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            Padding = new Padding(12, 12, 12, 0),
        };
        Controls.Add(instructionLabel);

        _birdCheckList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _birdCheckList.ItemCheck += (_, _) => BeginInvoke(RefreshTargetCombo);
        Controls.Add(_birdCheckList);

        var bottomPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 2, Height = 100, Padding = new Padding(12) };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomPanel.Controls.Add(new Label { Text = "残す鳥", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        _targetCombo = new ComboBox { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
        bottomPanel.Controls.Add(_targetCombo, 1, 0);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var closeButton = new Button { Text = "閉じる", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var mergeButton = new Button { Text = "統合する", Width = 100, Height = 32 };
        mergeButton.Click += (_, _) => TryMerge();
        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(mergeButton);
        bottomPanel.Controls.Add(buttonPanel, 0, 1);
        bottomPanel.SetColumnSpan(buttonPanel, 2);
        CancelButton = closeButton;

        Controls.Add(bottomPanel);
        // Dockの積み上げ順を安定させるため、下段パネルを最後に前面へ
        Controls.SetChildIndex(bottomPanel, 0);
        Controls.SetChildIndex(_birdCheckList, 1);
        Controls.SetChildIndex(instructionLabel, 2);
    }

    private void LoadBirds()
    {
        _birds = _birdRepository.GetAll();
        _birdCheckList.Items.Clear();
        foreach (var bird in _birds)
        {
            var reservationCount = _reservationRepository.GetByBird(bird.Id).Count;
            _birdCheckList.Items.Add(new BirdListItem(bird, reservationCount));
        }
        RefreshTargetCombo();
    }

    private record BirdListItem(Bird Bird, int ReservationCount)
    {
        public override string ToString() =>
            $"{Bird.Name}（{Bird.Species}・{Bird.OwnerName}） - 予約{ReservationCount}件";
    }

    private void RefreshTargetCombo()
    {
        var checkedBirds = _birdCheckList.CheckedItems.Cast<BirdListItem>().Select(i => i.Bird).ToList();
        var previousSelection = _targetCombo.SelectedItem as Bird;

        _targetCombo.Items.Clear();
        foreach (var bird in checkedBirds)
            _targetCombo.Items.Add(bird);

        if (previousSelection is not null && checkedBirds.Contains(previousSelection))
            _targetCombo.SelectedItem = previousSelection;
        else if (_targetCombo.Items.Count > 0)
            _targetCombo.SelectedIndex = 0;
    }

    private void TryMerge()
    {
        var checkedBirds = _birdCheckList.CheckedItems.Cast<BirdListItem>().Select(i => i.Bird).ToList();
        if (checkedBirds.Count < 2)
        {
            MessageBox.Show("統合する鳥を2件以上チェックしてください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_targetCombo.SelectedItem is not Bird target)
        {
            MessageBox.Show("残す鳥を選択してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var sources = checkedBirds.Where(b => b.Id != target.Id).ToList();
        var sourceNames = string.Join("、", sources.Select(b => b.Name));
        var confirm = MessageBox.Show(
            $"「{sourceNames}」を「{target.Name}」に統合します。\n統合された鳥の予約はすべて「{target.Name}」の予約になります。この操作は元に戻せません。よろしいですか？",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        foreach (var source in sources)
        {
            foreach (var reservation in _reservationRepository.GetByBird(source.Id))
            {
                reservation.BirdId = target.Id;
                _reservationRepository.Update(reservation);
            }
            _birdRepository.Delete(source.Id);
        }

        MessageBox.Show("統合しました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadBirds();
    }
}
