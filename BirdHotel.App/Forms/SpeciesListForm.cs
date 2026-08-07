using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class SpeciesListForm : Form
{
    private readonly SpeciesRepository _speciesRepository;
    private readonly BirdRepository _birdRepository;
    private DataGridView _grid = null!;
    private List<Species> _speciesList = new();

    public bool ChangesMade { get; private set; }

    public SpeciesListForm(SpeciesRepository speciesRepository, BirdRepository birdRepository)
    {
        _speciesRepository = speciesRepository;
        _birdRepository = birdRepository;
        BuildUi();
        RefreshGrid();
    }

    private void BuildUi()
    {
        Text = "種類の管理";
        Width = 480;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
        var addButton = new Button { Text = "新規登録", Width = 100, Height = 32 };
        addButton.Click += (_, _) => AddSpecies();
        var editButton = new Button { Text = "編集", Width = 100, Height = 32 };
        editButton.Click += (_, _) => EditSelectedSpecies();
        var deleteButton = new Button { Text = "削除", Width = 100, Height = 32 };
        deleteButton.Click += (_, _) => DeleteSelectedSpecies();
        var mergeButton = new Button { Text = "重複を統合", Width = 100, Height = 32, Margin = new Padding(20, 3, 3, 3) };
        mergeButton.Click += (_, _) =>
        {
            using var mergeForm = new SpeciesMergeForm(_speciesRepository, _birdRepository);
            mergeForm.ShowDialog(this);
            ChangesMade = true;
            RefreshGrid();
        };
        topPanel.Controls.Add(addButton);
        topPanel.Controls.Add(editButton);
        topPanel.Controls.Add(deleteButton);
        topPanel.Controls.Add(mergeButton);

        _grid = new DataGridView
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
        _grid.Columns.Add("Name", "種類名");
        foreach (DataGridViewColumn col in _grid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable; // 行の並びとリストの添字を一致させ続けるため
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelectedSpecies(); };

        Controls.Add(_grid);
        Controls.Add(topPanel);
    }

    private void RefreshGrid()
    {
        _speciesList = _speciesRepository.GetAll();
        _grid.Rows.Clear();
        foreach (var species in _speciesList)
            _grid.Rows.Add(species.Name);
    }

    private void AddSpecies()
    {
        using var editForm = new SpeciesEditForm(new Species());
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _speciesRepository.Insert(editForm.EditedSpecies);
            ChangesMade = true;
            RefreshGrid();
        }
    }

    private void EditSelectedSpecies()
    {
        var species = GetSelectedSpecies();
        if (species is null) return;

        using var editForm = new SpeciesEditForm(species);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _speciesRepository.Update(editForm.EditedSpecies);
            ChangesMade = true;
            RefreshGrid();
        }
    }

    private void DeleteSelectedSpecies()
    {
        var species = GetSelectedSpecies();
        if (species is null) return;

        var confirm = MessageBox.Show(
            $"「{species.Name}」を種類の一覧から削除しますか？\n（既に登録済みの鳥の種類はそのまま残ります）",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            _speciesRepository.Delete(species.Id);
            ChangesMade = true;
            RefreshGrid();
        }
    }

    private Species? GetSelectedSpecies()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        var index = _grid.SelectedRows[0].Index;
        return index >= 0 && index < _speciesList.Count ? _speciesList[index] : null;
    }
}
