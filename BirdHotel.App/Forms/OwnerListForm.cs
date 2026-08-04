using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class OwnerListForm : Form
{
    private readonly OwnerRepository _ownerRepository;
    private readonly BirdRepository _birdRepository;
    private DataGridView _grid = null!;
    private List<Owner> _owners = new();

    public OwnerListForm(OwnerRepository ownerRepository, BirdRepository birdRepository)
    {
        _ownerRepository = ownerRepository;
        _birdRepository = birdRepository;
        BuildUi();
        RefreshGrid();
    }

    private void BuildUi()
    {
        Text = "飼い主の管理";
        Width = 760;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
        var addButton = new Button { Text = "新規登録", Width = 100, Height = 32 };
        addButton.Click += (_, _) => AddOwner();
        var editButton = new Button { Text = "編集", Width = 100, Height = 32 };
        editButton.Click += (_, _) => EditSelectedOwner();
        var deleteButton = new Button { Text = "削除", Width = 100, Height = 32 };
        deleteButton.Click += (_, _) => DeleteSelectedOwner();
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
        _grid.Columns.Add("Name", "飼い主名");
        _grid.Columns.Add("Contact", "連絡先");
        _grid.Columns.Add("Proprietor", "経営者本人");
        _grid.Columns.Add("BirdCount", "登録羽数");
        _grid.Columns.Add("Notes", "備考");
        foreach (DataGridViewColumn col in _grid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable; // 行の並びとリストの添字を一致させ続けるため
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelectedOwner(); };

        Controls.Add(_grid);
        Controls.Add(topPanel);
    }

    private void RefreshGrid()
    {
        _owners = _ownerRepository.GetAll();
        var birds = _birdRepository.GetAll();

        _grid.Rows.Clear();
        foreach (var owner in _owners)
        {
            var birdCount = birds.Count(b => b.OwnerId == owner.Id);
            _grid.Rows.Add(owner.Name, owner.Contact, owner.IsProprietor ? "○" : "", birdCount, owner.Notes);
        }
    }

    private void AddOwner()
    {
        using var editForm = new OwnerEditForm(new Owner());
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _ownerRepository.Insert(editForm.EditedOwner);
            RefreshGrid();
        }
    }

    private void EditSelectedOwner()
    {
        var owner = GetSelectedOwner();
        if (owner is null) return;

        using var editForm = new OwnerEditForm(owner);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _ownerRepository.Update(editForm.EditedOwner);
            RefreshGrid();
        }
    }

    private void DeleteSelectedOwner()
    {
        var owner = GetSelectedOwner();
        if (owner is null) return;

        var birdCount = _birdRepository.GetByOwner(owner.Id).Count;
        if (birdCount > 0)
        {
            MessageBox.Show($"「{owner.Name}」には登録済みの鳥が{birdCount}羽あるため削除できません。先に鳥の飼い主を変更するか、鳥を削除してください。",
                "削除できません", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show($"「{owner.Name}」を削除しますか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            _ownerRepository.Delete(owner.Id);
            RefreshGrid();
        }
    }

    private Owner? GetSelectedOwner()
    {
        if (_grid.SelectedRows.Count == 0) return null;
        var index = _grid.SelectedRows[0].Index;
        return index >= 0 && index < _owners.Count ? _owners[index] : null;
    }
}
