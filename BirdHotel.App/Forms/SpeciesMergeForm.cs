using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class SpeciesMergeForm : Form
{
    private readonly SpeciesRepository _speciesRepository;
    private readonly BirdRepository _birdRepository;
    private List<Species> _speciesList = new();

    private CheckedListBox _speciesCheckList = null!;
    private ComboBox _targetCombo = null!;

    public SpeciesMergeForm(SpeciesRepository speciesRepository, BirdRepository birdRepository)
    {
        _speciesRepository = speciesRepository;
        _birdRepository = birdRepository;
        BuildUi();
        LoadSpecies();
    }

    private void BuildUi()
    {
        Text = "種類の重複統合";
        Width = 460;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var instructionLabel = new Label
        {
            Text = "表記ゆれ等で別々に登録されてしまった種類をまとめます。\n" +
                   "同じ種類だと思うものに2件以上チェックを付け、下で「残す種類」を選んでください。\n" +
                   "チェックした他の種類が入力されている鳥はすべて「残す種類」に付け替えられ、重複していた種類は削除されます（元に戻せません）。",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Padding = new Padding(12, 12, 12, 0),
        };
        Controls.Add(instructionLabel);

        _speciesCheckList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        _speciesCheckList.ItemCheck += (_, _) => BeginInvoke(RefreshTargetCombo);
        Controls.Add(_speciesCheckList);

        var bottomPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 2, Height = 100, Padding = new Padding(12) };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomPanel.Controls.Add(new Label { Text = "残す種類", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        _targetCombo = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
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
        Controls.SetChildIndex(_speciesCheckList, 1);
        Controls.SetChildIndex(instructionLabel, 2);
    }

    private void LoadSpecies()
    {
        _speciesList = _speciesRepository.GetAll();
        var birds = _birdRepository.GetAll();

        _speciesCheckList.Items.Clear();
        foreach (var species in _speciesList)
        {
            var birdCount = birds.Count(b => b.Species == species.Name);
            _speciesCheckList.Items.Add(new SpeciesListItem(species, birdCount));
        }
        RefreshTargetCombo();
    }

    private record SpeciesListItem(Species Species, int BirdCount)
    {
        public override string ToString() => $"{Species.Name} - 鳥{BirdCount}羽";
    }

    private void RefreshTargetCombo()
    {
        var checkedSpecies = _speciesCheckList.CheckedItems.Cast<SpeciesListItem>().Select(i => i.Species).ToList();
        var previousSelection = _targetCombo.SelectedItem as Species;

        _targetCombo.Items.Clear();
        foreach (var species in checkedSpecies)
            _targetCombo.Items.Add(species);

        if (previousSelection is not null && checkedSpecies.Contains(previousSelection))
            _targetCombo.SelectedItem = previousSelection;
        else if (_targetCombo.Items.Count > 0)
            _targetCombo.SelectedIndex = 0;
    }

    private void TryMerge()
    {
        var checkedSpecies = _speciesCheckList.CheckedItems.Cast<SpeciesListItem>().Select(i => i.Species).ToList();
        if (checkedSpecies.Count < 2)
        {
            MessageBox.Show("統合する種類を2件以上チェックしてください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_targetCombo.SelectedItem is not Species target)
        {
            MessageBox.Show("残す種類を選択してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var sources = checkedSpecies.Where(s => s.Id != target.Id).ToList();
        var sourceNames = string.Join("、", sources.Select(s => s.Name));
        var confirm = MessageBox.Show(
            $"「{sourceNames}」を「{target.Name}」に統合します。\nそれらの種類が入力されている鳥はすべて「{target.Name}」になります。この操作は元に戻せません。よろしいですか？",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        var birds = _birdRepository.GetAll();
        foreach (var source in sources)
        {
            foreach (var bird in birds.Where(b => b.Species == source.Name))
            {
                bird.Species = target.Name;
                _birdRepository.Update(bird);
            }
            _speciesRepository.Delete(source.Id);
        }

        MessageBox.Show("統合しました。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        LoadSpecies();
    }
}
