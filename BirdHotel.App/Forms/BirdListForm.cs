using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class BirdListForm : Form
{
    private readonly BirdRepository _birdRepository;
    private readonly OwnerRepository _ownerRepository;
    private DataGridView _grid = null!;
    private List<Bird> _birds = new();

    public BirdListForm(BirdRepository birdRepository, OwnerRepository ownerRepository)
    {
        _birdRepository = birdRepository;
        _ownerRepository = ownerRepository;
        BuildUi();
        RefreshGrid();
    }

    private void BuildUi()
    {
        Text = "鳥の管理";
        Width = 900;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
        var addButton = new Button { Text = "新規登録", Width = 100, Height = 32 };
        addButton.Click += (_, _) => AddBird();
        var editButton = new Button { Text = "編集", Width = 100, Height = 32 };
        editButton.Click += (_, _) => EditSelectedBird();
        var deleteButton = new Button { Text = "削除", Width = 100, Height = 32 };
        deleteButton.Click += (_, _) => DeleteSelectedBird();
        var ownerButton = new Button { Text = "飼い主の管理", Width = 110, Height = 32, Margin = new Padding(20, 3, 3, 3) };
        ownerButton.Click += (_, _) =>
        {
            using var ownerForm = new OwnerListForm(_ownerRepository, _birdRepository);
            ownerForm.ShowDialog(this);
            RefreshGrid();
        };
        topPanel.Controls.Add(addButton);
        topPanel.Controls.Add(editButton);
        topPanel.Controls.Add(deleteButton);
        topPanel.Controls.Add(ownerButton);

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
        _grid.Columns.Add("Species", "種類");
        _grid.Columns.Add("Name", "名前");
        _grid.Columns.Add("BirthDate", "生年月日");
        _grid.Columns.Add("Size", "型");
        _grid.Columns.Add("Gender", "性別");
        _grid.Columns.Add("Owner", "飼い主");
        _grid.Columns.Add("Proprietor", "経営者本人");
        foreach (DataGridViewColumn col in _grid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable; // 行の並びとリストの添字を一致させ続けるため
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelectedBird(); };

        Controls.Add(_grid);
        Controls.Add(topPanel);
    }

    private void RefreshGrid()
    {
        _birds = _birdRepository.GetAll();
        _grid.Rows.Clear();
        foreach (var bird in _birds)
        {
            _grid.Rows.Add(
                bird.Species,
                bird.Name,
                bird.BirthDate?.ToString("yyyy-MM-dd") ?? "不明",
                bird.Size.ToString(),
                bird.Gender.ToString(),
                bird.OwnerName,
                bird.IsProprietorBird ? "○" : "");
        }
    }

    private void AddBird()
    {
        if (_ownerRepository.GetAll().Count == 0)
        {
            MessageBox.Show("先に飼い主を1件以上登録してください（「飼い主の管理」から登録できます）。", "飼い主未登録", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var editForm = new BirdEditForm(new Bird(), _ownerRepository);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _birdRepository.Insert(editForm.Bird);
            RefreshGrid();
        }
    }

    private void EditSelectedBird()
    {
        var bird = GetSelectedBird();
        if (bird is null) return;

        using var editForm = new BirdEditForm(bird, _ownerRepository);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _birdRepository.Update(editForm.Bird);
            RefreshGrid();
        }
    }

    private void DeleteSelectedBird()
    {
        var bird = GetSelectedBird();
        if (bird is null) return;

        var confirm = MessageBox.Show($"「{bird.Name}」を削除しますか？関連する予約も削除されます。", "確認",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            _birdRepository.Delete(bird.Id);
            RefreshGrid();
        }
    }

    private Bird? GetSelectedBird()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        var index = _grid.SelectedRows[0].Index;
        return index >= 0 && index < _birds.Count ? _birds[index] : null;
    }
}
