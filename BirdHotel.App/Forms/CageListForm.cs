using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class CageListForm : Form
{
    private readonly CageRepository _cageRepository;
    private DataGridView _grid = null!;
    private List<Cage> _cages = new();

    public CageListForm(CageRepository cageRepository)
    {
        _cageRepository = cageRepository;
        BuildUi();
        RefreshGrid();
    }

    private void BuildUi()
    {
        Text = "籠の管理";
        Width = 620;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
        var addButton = new Button { Text = "新規登録", Width = 100, Height = 32 };
        addButton.Click += (_, _) => AddCage();
        var editButton = new Button { Text = "編集", Width = 100, Height = 32 };
        editButton.Click += (_, _) => EditSelectedCage();
        var deleteButton = new Button { Text = "削除", Width = 100, Height = 32 };
        deleteButton.Click += (_, _) => DeleteSelectedCage();
        topPanel.Controls.Add(addButton);
        topPanel.Controls.Add(editButton);
        topPanel.Controls.Add(deleteButton);

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
        _grid.Columns.Add("Name", "籠名");
        _grid.Columns.Add("Capacity", "定員（既定2、特別時は変更可）");
        _grid.Columns.Add("Type", "種別");
        _grid.Columns.Add("Group", "グループ");
        _grid.Columns.Add("Notes", "備考");
        foreach (DataGridViewColumn col in _grid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable; // 行の並びとリストの添字を一致させ続けるため
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelectedCage(); };

        Controls.Add(_grid);
        Controls.Add(topPanel);
    }

    private void RefreshGrid()
    {
        _cages = _cageRepository.GetAll();
        _grid.Rows.Clear();
        foreach (var cage in _cages)
            _grid.Rows.Add(cage.Name, cage.Capacity, cage.Type.ToString(), cage.GroupName, cage.Notes);
    }

    private List<string> GetExistingGroupNames() =>
        _cages.Select(c => c.GroupName).Where(g => g.Length > 0).Distinct().ToList();

    private void AddCage()
    {
        using var editForm = new CageEditForm(new Cage { Capacity = 2 }, GetExistingGroupNames());
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _cageRepository.Insert(editForm.Cage);
            RefreshGrid();
        }
    }

    private void EditSelectedCage()
    {
        var cage = GetSelectedCage();
        if (cage is null) return;

        using var editForm = new CageEditForm(cage, GetExistingGroupNames());
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _cageRepository.Update(editForm.Cage);
            RefreshGrid();
        }
    }

    private void DeleteSelectedCage()
    {
        var cage = GetSelectedCage();
        if (cage is null) return;

        var confirm = MessageBox.Show($"「{cage.Name}」を削除しますか？関連する予約も削除されます。", "確認",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            _cageRepository.Delete(cage.Id);
            RefreshGrid();
        }
    }

    private Cage? GetSelectedCage()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        var index = _grid.SelectedRows[0].Index;
        return index >= 0 && index < _cages.Count ? _cages[index] : null;
    }
}
