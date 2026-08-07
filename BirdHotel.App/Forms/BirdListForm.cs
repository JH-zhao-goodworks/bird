using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class BirdListForm : Form
{
    private readonly BirdRepository _birdRepository;
    private readonly OwnerRepository _ownerRepository;
    private readonly SpeciesRepository _speciesRepository;
    private readonly ReservationRepository _reservationRepository;

    private FlowLayoutPanel _cardsPanel = null!;
    private List<Bird> _birds = new();
    private Bird? _selectedBird;

    public BirdListForm(BirdRepository birdRepository, OwnerRepository ownerRepository, SpeciesRepository speciesRepository, ReservationRepository reservationRepository)
    {
        _birdRepository = birdRepository;
        _ownerRepository = ownerRepository;
        _speciesRepository = speciesRepository;
        _reservationRepository = reservationRepository;
        BuildUi();
        RefreshCards();
    }

    private void BuildUi()
    {
        Text = "鳥の管理";
        Width = 960;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
        var addButton = new Button { Text = "新規登録", Width = 100, Height = 32 };
        addButton.Click += (_, _) => AddBird();
        var deleteButton = new Button { Text = "削除", Width = 100, Height = 32 };
        deleteButton.Click += (_, _) => DeleteSelectedBird();
        var ownerButton = new Button { Text = "飼い主の管理", Width = 110, Height = 32, Margin = new Padding(20, 3, 3, 3) };
        ownerButton.Click += (_, _) =>
        {
            using var ownerForm = new OwnerListForm(_ownerRepository, _birdRepository);
            ownerForm.ShowDialog(this);
            RefreshCards();
        };
        var speciesButton = new Button { Text = "種類の管理", Width = 110, Height = 32 };
        speciesButton.Click += (_, _) =>
        {
            using var speciesForm = new SpeciesListForm(_speciesRepository, _birdRepository);
            speciesForm.ShowDialog(this);
            RefreshCards();
        };
        var bulkImportButton = new Button { Text = "一括登録", Width = 100, Height = 32, Margin = new Padding(20, 3, 3, 3) };
        bulkImportButton.Click += (_, _) =>
        {
            using var bulkForm = new BirdBulkImportForm(_birdRepository, _ownerRepository, _speciesRepository);
            bulkForm.ShowDialog(this);
            RefreshCards();
        };
        var mergeButton = new Button { Text = "重複を統合", Width = 100, Height = 32 };
        mergeButton.Click += (_, _) =>
        {
            using var mergeForm = new BirdMergeForm(_birdRepository, _reservationRepository);
            mergeForm.ShowDialog(this);
            RefreshCards();
        };
        var bulkDeleteButton = new Button { Text = "一括整理", Width = 100, Height = 32 };
        bulkDeleteButton.Click += (_, _) =>
        {
            using var bulkDeleteForm = new BirdBulkDeleteForm(_birdRepository, _ownerRepository, _reservationRepository);
            bulkDeleteForm.ShowDialog(this);
            _selectedBird = null;
            RefreshCards();
        };

        topPanel.Controls.Add(addButton);
        topPanel.Controls.Add(deleteButton);
        topPanel.Controls.Add(ownerButton);
        topPanel.Controls.Add(speciesButton);
        topPanel.Controls.Add(bulkImportButton);
        topPanel.Controls.Add(mergeButton);
        topPanel.Controls.Add(bulkDeleteButton);

        var hintLabel = new Label
        {
            Text = "※ クリックして詳細確認・編集（ペア名ごとにまとまって表示されます）",
            Dock = DockStyle.Top,
            Height = 24,
            Padding = new Padding(12, 4, 0, 0),
            ForeColor = Color.DimGray,
        };

        _cardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            FlowDirection = FlowDirection.LeftToRight,
        };

        Controls.Add(_cardsPanel);
        Controls.Add(hintLabel);
        Controls.Add(topPanel);
    }

    private void RefreshCards()
    {
        _cardsPanel.SuspendLayout();
        _cardsPanel.Controls.Clear();

        _birds = _birdRepository.GetAll();

        // ペア可の鳥はペア名ごとにまとめ、ペア不可の鳥は1羽ずつ独立したカードにする
        var groups = new List<List<Bird>>();
        var pairGroups = new Dictionary<string, List<Bird>>(StringComparer.Ordinal);

        foreach (var bird in _birds)
        {
            if (bird.CanPair && bird.PairName.Length > 0)
            {
                if (!pairGroups.TryGetValue(bird.PairName, out var group))
                {
                    group = new List<Bird>();
                    pairGroups[bird.PairName] = group;
                    groups.Add(group);
                }
                group.Add(bird);
            }
            else
            {
                groups.Add([bird]);
            }
        }

        foreach (var group in groups)
            _cardsPanel.Controls.Add(BuildPairCard(group));

        _cardsPanel.ResumeLayout();
    }

    private Control BuildPairCard(List<Bird> birds)
    {
        var card = new Panel
        {
            Width = 420,
            Height = 200,
            Margin = new Padding(8),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(8),
        };

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowTemplate = { Height = 28 },
            Cursor = Cursors.Hand,
        };
        grid.Columns.Add("Name", "名前");
        grid.Columns.Add("CanPair", "ペア");
        grid.Columns.Add("PairName", "ペア名");
        grid.Columns.Add("Owner", "飼い主");
        foreach (DataGridViewColumn col in grid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable; // 行の並びとリストの添字を一致させ続けるため

        foreach (var bird in birds)
        {
            grid.Rows.Add(
                bird.Name,
                bird.CanPair ? "可" : "不可",
                bird.CanPair && bird.PairName.Length > 0 ? bird.PairName : "X",
                bird.OwnerName);
        }

        grid.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= birds.Count) return;
            _selectedBird = birds[e.RowIndex];
            OpenBirdDetail(birds[e.RowIndex]);
        };

        card.Controls.Add(grid);
        return card;
    }

    private void OpenBirdDetail(Bird bird)
    {
        using var editForm = new BirdEditForm(bird, _ownerRepository, _speciesRepository);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _birdRepository.Update(editForm.Bird);
            RefreshCards();
        }
    }

    private void AddBird()
    {
        if (_ownerRepository.GetAll().Count == 0)
        {
            MessageBox.Show("先に飼い主を1件以上登録してください（「飼い主の管理」から登録できます）。", "飼い主未登録", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var editForm = new BirdEditForm(new Bird(), _ownerRepository, _speciesRepository);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _birdRepository.Insert(editForm.Bird);
            RefreshCards();
        }
    }

    private void DeleteSelectedBird()
    {
        if (_selectedBird is null)
        {
            MessageBox.Show("先に削除する鳥をクリックして選んでください。", "削除", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show($"「{_selectedBird.Name}」を削除しますか？関連する予約も削除されます。", "確認",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            _birdRepository.Delete(_selectedBird.Id);
            _selectedBird = null;
            RefreshCards();
        }
    }
}
