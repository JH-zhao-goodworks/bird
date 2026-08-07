using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class MainForm : Form
{
    private static readonly Color[] OwnerColorPalette =
    [
        Color.FromArgb(255, 224, 178), // オレンジ
        Color.FromArgb(198, 224, 255), // 水色
        Color.FromArgb(198, 239, 206), // 黄緑
        Color.FromArgb(255, 235, 156), // 黄色
        Color.FromArgb(230, 208, 255), // 紫
        Color.FromArgb(255, 205, 210), // ピンク
        Color.FromArgb(224, 224, 224), // グレー
    ];

    private readonly BirdRepository _birdRepository;
    private readonly CageRepository _cageRepository;
    private readonly ReservationRepository _reservationRepository;
    private readonly OwnerRepository _ownerRepository;
    private readonly SpeciesRepository _speciesRepository;

    private FlowLayoutPanel _cardsPanel = null!;

    public MainForm(BirdRepository birdRepository, CageRepository cageRepository, ReservationRepository reservationRepository, OwnerRepository ownerRepository, SpeciesRepository speciesRepository)
    {
        _birdRepository = birdRepository;
        _cageRepository = cageRepository;
        _reservationRepository = reservationRepository;
        _ownerRepository = ownerRepository;
        _speciesRepository = speciesRepository;

        BuildUi();
        RefreshCards();
    }

    private void BuildUi()
    {
        Text = "小鳥ホテル 籠一覧";
        Width = 1040;
        Height = 680;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Yu Gothic UI", 10F);

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(10),
            AutoSize = false,
        };

        var ownerButton = new Button { Text = "飼い主の管理", Width = 120, Height = 36 };
        ownerButton.Click += (_, _) => OpenOwnerList();

        var birdButton = new Button { Text = "鳥の管理", Width = 120, Height = 36 };
        birdButton.Click += (_, _) => OpenBirdList();

        var cageButton = new Button { Text = "籠の管理", Width = 120, Height = 36 };
        cageButton.Click += (_, _) => OpenCageList();

        var reservationButton = new Button { Text = "予約登録・空き確認", Width = 160, Height = 36 };
        reservationButton.Click += (_, _) => OpenReservationForm();

        var bulkReservationButton = new Button { Text = "予約の一括登録", Width = 130, Height = 36 };
        bulkReservationButton.Click += (_, _) => OpenReservationBulkImport();

        var clearAllButton = new Button { Text = "全籠クリア", Width = 100, Height = 36, Margin = new Padding(20, 3, 3, 3) };
        clearAllButton.Click += (_, _) => ClearAllCages();

        var refreshButton = new Button { Text = "更新", Width = 80, Height = 36 };
        refreshButton.Click += (_, _) => RefreshCards();

        topPanel.Controls.Add(ownerButton);
        topPanel.Controls.Add(birdButton);
        topPanel.Controls.Add(cageButton);
        topPanel.Controls.Add(reservationButton);
        topPanel.Controls.Add(bulkReservationButton);
        topPanel.Controls.Add(clearAllButton);
        topPanel.Controls.Add(refreshButton);

        _cardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            FlowDirection = FlowDirection.LeftToRight,
        };

        Controls.Add(_cardsPanel);
        Controls.Add(topPanel);
    }

    private void RefreshCards()
    {
        _cardsPanel.SuspendLayout();
        _cardsPanel.Controls.Clear();

        var cages = _cageRepository.GetAll();
        var allReservations = _reservationRepository.GetAll();

        foreach (var cage in cages)
        {
            var reservations = allReservations
                .Where(r => r.CageId == cage.Id)
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.BirdName)
                .ToList();
            _cardsPanel.Controls.Add(BuildCageCard(cage, reservations));
        }

        _cardsPanel.ResumeLayout();
    }

    private Control BuildCageCard(Cage cage, List<Reservation> reservations)
    {
        var maxConcurrent = 0;
        foreach (var r in reservations)
        {
            var concurrent = reservations.Count(other => r.OverlapsWith(other.StartDate, other.EndDate));
            maxConcurrent = Math.Max(maxConcurrent, concurrent);
        }

        var card = new Panel
        {
            Width = 420,
            Height = 260,
            Margin = new Padding(8),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(8),
        };

        var specialText = maxConcurrent > cage.Capacity ? $"　（特{maxConcurrent}）" : "";
        var typeText = cage.Type == CageType.通常籠 ? "" : $"　[{cage.Type}]";
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 26 };
        var headerLabel = new Label
        {
            Text = $"{cage.Name}　定員{cage.Capacity}{typeText}{specialText}",
            Dock = DockStyle.Fill,
            Font = new Font("Yu Gothic UI", 11F, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        var moveButton = new Button { Text = "移動", Dock = DockStyle.Right, Width = 60, Font = new Font("Yu Gothic UI", 8F) };
        moveButton.Click += (_, _) => OpenCageMove(cage);
        var clearButton = new Button { Text = "クリア", Dock = DockStyle.Right, Width = 60, Font = new Font("Yu Gothic UI", 8F) };
        clearButton.Click += (_, _) => ClearCage(cage);
        headerPanel.Controls.Add(headerLabel);
        headerPanel.Controls.Add(moveButton);
        headerPanel.Controls.Add(clearButton);

        void OpenBooking(object? sender, EventArgs e) => OpenCageBooking(cage);
        headerLabel.Click += OpenBooking;
        card.Cursor = Cursors.Hand;
        card.Click += OpenBooking;

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowTemplate = { Height = 28 },
        };
        grid.Columns.Add("Name", "名前");
        grid.Columns.Add("Start", "開始日");
        grid.Columns.Add("End", "終了日");
        grid.Columns.Add("Total", "合計");
        foreach (DataGridViewColumn col in grid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Click += OpenBooking;

        var ownerColors = new Dictionary<int, Color>();
        var nextColorIndex = 0;
        Color ColorForOwner(int? ownerId)
        {
            var key = ownerId ?? -1;
            if (!ownerColors.TryGetValue(key, out var color))
            {
                color = OwnerColorPalette[nextColorIndex % OwnerColorPalette.Length];
                nextColorIndex++;
                ownerColors[key] = color;
            }
            return color;
        }

        foreach (var r in reservations)
        {
            string startText, endText, totalText;
            if (r.IsIndefinite)
            {
                startText = endText = totalText = "無期限";
            }
            else
            {
                startText = r.StartDate.ToString("yyyy/MM/dd");
                endText = r.EndDate!.Value.ToString("yyyy/MM/dd");
                totalText = $"{(r.EndDate.Value - r.StartDate).Days}日間";
            }

            var rowIndex = grid.Rows.Add(r.BirdName, startText, endText, totalText);
            grid.Rows[rowIndex].DefaultCellStyle.BackColor = ColorForOwner(r.OwnerId);
        }

        card.Controls.Add(grid);
        card.Controls.Add(headerPanel);
        return card;
    }

    private void OpenCageMove(Cage cage)
    {
        var reservations = _reservationRepository.GetByCage(cage.Id);
        if (reservations.Count == 0)
        {
            MessageBox.Show($"「{cage.Name}」には移動できる鳥がいません。", "移動", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new CageMoveForm(cage, _cageRepository, _reservationRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void ClearAllCages()
    {
        var reservations = _reservationRepository.GetAll();
        if (reservations.Count == 0)
        {
            MessageBox.Show("取り消せる予約がありません。", "全籠クリア", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"すべての籠の予約{reservations.Count}件を取り消します。よろしいですか？（元に戻せません）",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        foreach (var reservation in reservations)
            _reservationRepository.Delete(reservation.Id);

        RefreshCards();
    }

    private void ClearCage(Cage cage)
    {
        var reservations = _reservationRepository.GetByCage(cage.Id);
        if (reservations.Count == 0)
        {
            MessageBox.Show($"「{cage.Name}」には予約がありません。", "クリア", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"「{cage.Name}」の予約を{reservations.Count}件すべて取り消します。よろしいですか？（元に戻せません）",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        foreach (var reservation in reservations)
            _reservationRepository.Delete(reservation.Id);

        RefreshCards();
    }

    private void OpenOwnerList()
    {
        using var form = new OwnerListForm(_ownerRepository, _birdRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenBirdList()
    {
        using var form = new BirdListForm(_birdRepository, _ownerRepository, _speciesRepository, _reservationRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenCageList()
    {
        using var form = new CageListForm(_cageRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenReservationForm()
    {
        using var form = new ReservationForm(_birdRepository, _cageRepository, _reservationRepository, _ownerRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenReservationBulkImport()
    {
        using var form = new ReservationBulkImportForm(_birdRepository, _ownerRepository, _speciesRepository, _cageRepository, _reservationRepository);
        form.ShowDialog(this);
        RefreshCards();
    }

    private void OpenCageBooking(Cage cage)
    {
        using var form = new CageBookingForm(cage, _birdRepository, _reservationRepository, _ownerRepository);
        form.ShowDialog(this);
        RefreshCards();
    }
}
