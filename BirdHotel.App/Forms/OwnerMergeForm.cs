using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class OwnerMergeForm : Form
{
    private readonly OwnerRepository _ownerRepository;
    private readonly BirdRepository _birdRepository;
    private List<Owner> _owners = new();

    private CheckedListBox _ownerCheckList = null!;
    private ComboBox _targetCombo = null!;

    public OwnerMergeForm(OwnerRepository ownerRepository, BirdRepository birdRepository)
    {
        _ownerRepository = ownerRepository;
        _birdRepository = birdRepository;
        BuildUi();
        LoadOwners();
    }

    private void BuildUi()
    {
        Text = "飼い主の重複統合";
        Width = 480;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var instructionLabel = new Label
        {
            Text = "同一人物なのに別々に登録されてしまった飼い主をまとめます。\n" +
                   "同じ人だと思う飼い主に2件以上チェックを付け、下で「残す飼い主」を選んでください。\n" +
                   "チェックした他の飼い主に登録されている鳥はすべて「残す飼い主」に付け替えられ、重複していた飼い主は削除されます（元に戻せません）。",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            Padding = new Padding(12, 12, 12, 0),
        };
        Controls.Add(instructionLabel);

        _ownerCheckList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _ownerCheckList.ItemCheck += (_, _) => BeginInvoke(RefreshTargetCombo);
        Controls.Add(_ownerCheckList);

        var bottomPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 2, Height = 100, Padding = new Padding(12) };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomPanel.Controls.Add(new Label { Text = "残す飼い主", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        _targetCombo = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
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
        Controls.SetChildIndex(_ownerCheckList, 1);
        Controls.SetChildIndex(instructionLabel, 2);
    }

    private void LoadOwners()
    {
        _owners = _ownerRepository.GetAll();
        _ownerCheckList.Items.Clear();
        foreach (var owner in _owners)
        {
            var birdCount = _birdRepository.GetByOwner(owner.Id).Count;
            _ownerCheckList.Items.Add(new OwnerListItem(owner, birdCount));
        }
        RefreshTargetCombo();
    }

    private record OwnerListItem(Owner Owner, int BirdCount)
    {
        public override string ToString() =>
            $"{Owner.Name}" + (Owner.Contact.Length > 0 ? $"（{Owner.Contact}）" : "") + $" - 鳥{BirdCount}羽";
    }

    private void RefreshTargetCombo()
    {
        var checkedOwners = _ownerCheckList.CheckedItems.Cast<OwnerListItem>().Select(i => i.Owner).ToList();
        var previousSelection = _targetCombo.SelectedItem as Owner;

        _targetCombo.Items.Clear();
        foreach (var owner in checkedOwners)
            _targetCombo.Items.Add(owner);

        if (previousSelection is not null && checkedOwners.Contains(previousSelection))
            _targetCombo.SelectedItem = previousSelection;
        else if (_targetCombo.Items.Count > 0)
            _targetCombo.SelectedIndex = 0;
    }

    private void TryMerge()
    {
        var checkedOwners = _ownerCheckList.CheckedItems.Cast<OwnerListItem>().Select(i => i.Owner).ToList();
        if (checkedOwners.Count < 2)
        {
            MessageBox.Show("統合する飼い主を2件以上チェックしてください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_targetCombo.SelectedItem is not Owner target)
        {
            MessageBox.Show("残す飼い主を選択してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var sources = checkedOwners.Where(o => o.Id != target.Id).ToList();
        var sourceNames = string.Join("、", sources.Select(o => o.Name));
        var confirm = MessageBox.Show(
            $"「{sourceNames}」を「{target.Name}」に統合します。\n統合された飼い主の鳥はすべて「{target.Name}」の鳥になります。この操作は元に戻せません。よろしいですか？",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        foreach (var source in sources)
        {
            foreach (var bird in _birdRepository.GetByOwner(source.Id))
            {
                bird.OwnerId = target.Id;
                _birdRepository.Update(bird);
            }
            _ownerRepository.Delete(source.Id);
        }

        MessageBox.Show("統合しました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadOwners();
    }
}
