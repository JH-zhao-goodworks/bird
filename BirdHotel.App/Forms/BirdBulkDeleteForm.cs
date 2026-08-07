using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class BirdBulkDeleteForm : Form
{
    private readonly BirdRepository _birdRepository;
    private readonly OwnerRepository _ownerRepository;
    private readonly ReservationRepository _reservationRepository;

    private List<Bird> _birds = new();
    private List<Owner> _owners = new();

    private ComboBox _ownerFilterCombo = null!;
    private CheckedListBox _birdCheckList = null!;
    private Label _summaryLabel = null!;

    public BirdBulkDeleteForm(BirdRepository birdRepository, OwnerRepository ownerRepository, ReservationRepository reservationRepository)
    {
        _birdRepository = birdRepository;
        _ownerRepository = ownerRepository;
        _reservationRepository = reservationRepository;
        BuildUi();
        LoadLookups();
    }

    private void BuildUi()
    {
        Text = "鳥の一括整理";
        Width = 560;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var instructionLabel = new Label
        {
            Text = "削除したい鳥にチェックを付けて「選択した鳥を削除」を押してください。\n" +
                   "削除すると、その鳥の予約もすべて削除されます（元に戻せません）。",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Padding = new Padding(12, 12, 12, 0),
        };
        Controls.Add(instructionLabel);

        var filterPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(12, 6, 12, 0) };
        filterPanel.Controls.Add(new Label { Text = "飼い主で絞り込み", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 4, 0) });
        _ownerFilterCombo = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _ownerFilterCombo.SelectedIndexChanged += (_, _) => RefreshBirdCheckList();
        filterPanel.Controls.Add(_ownerFilterCombo);

        var selectAllButton = new Button { Text = "全選択", Width = 80, Height = 26, Margin = new Padding(16, 2, 4, 0) };
        selectAllButton.Click += (_, _) => SetAllChecked(true);
        var clearAllButton = new Button { Text = "全解除", Width = 80, Height = 26, Margin = new Padding(0, 2, 0, 0) };
        clearAllButton.Click += (_, _) => SetAllChecked(false);
        filterPanel.Controls.Add(selectAllButton);
        filterPanel.Controls.Add(clearAllButton);
        Controls.Add(filterPanel);

        _birdCheckList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _birdCheckList.ItemCheck += (_, _) => BeginInvoke(RefreshSummary);
        Controls.Add(_birdCheckList);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50, Padding = new Padding(10) };
        var closeButton = new Button { Text = "閉じる", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var deleteButton = new Button { Text = "選択した鳥を削除", Width = 150, Height = 32 };
        deleteButton.Click += (_, _) => TryDelete();
        _summaryLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(10, 10, 10, 0) };
        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(deleteButton);
        buttonPanel.Controls.Add(_summaryLabel);
        CancelButton = closeButton;
        Controls.Add(buttonPanel);

        // Dockの積み上げ順を安定させるため、明示的に並べ替え
        Controls.SetChildIndex(_birdCheckList, 0);
        Controls.SetChildIndex(buttonPanel, 1);
        Controls.SetChildIndex(filterPanel, 2);
        Controls.SetChildIndex(instructionLabel, 3);
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
        {
            var reservationCount = _reservationRepository.GetByBird(bird.Id).Count;
            _birdCheckList.Items.Add(new BirdListItem(bird, reservationCount));
        }

        RefreshSummary();
    }

    private record BirdListItem(Bird Bird, int ReservationCount)
    {
        public override string ToString()
        {
            var pair = Bird.CanPair && Bird.PairName.Length > 0 ? Bird.PairName : "ペア不可";
            return $"{Bird.Name}（{Bird.Species}・{Bird.OwnerName}・{pair}） - 予約{ReservationCount}件";
        }
    }

    private void SetAllChecked(bool value)
    {
        for (var i = 0; i < _birdCheckList.Items.Count; i++)
            _birdCheckList.SetItemChecked(i, value);
        RefreshSummary();
    }

    private List<BirdListItem> GetCheckedItems() => _birdCheckList.CheckedItems.Cast<BirdListItem>().ToList();

    private void RefreshSummary()
    {
        var checkedItems = GetCheckedItems();
        var reservationCount = checkedItems.Sum(i => i.ReservationCount);
        _summaryLabel.Text = $"{_birdCheckList.Items.Count}羽中 {checkedItems.Count}羽を選択中（予約{reservationCount}件も削除されます）";
    }

    private void TryDelete()
    {
        var checkedItems = GetCheckedItems();
        if (checkedItems.Count == 0)
        {
            MessageBox.Show("削除する鳥にチェックを付けてください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var reservationCount = checkedItems.Sum(i => i.ReservationCount);
        var names = string.Join("、", checkedItems.Take(10).Select(i => i.Bird.Name));
        if (checkedItems.Count > 10) names += " ほか";

        var confirm = MessageBox.Show(
            $"{checkedItems.Count}羽（{names}）を削除します。関連する予約{reservationCount}件も削除されます。\nこの操作は元に戻せません。よろしいですか？",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        foreach (var item in checkedItems)
            _birdRepository.Delete(item.Bird.Id);

        MessageBox.Show($"{checkedItems.Count}羽を削除しました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _birds = _birdRepository.GetAll();
        RefreshBirdCheckList();
    }
}
