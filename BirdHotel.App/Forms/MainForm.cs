using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class MainForm : Form
{
    private readonly BirdRepository _birdRepository;
    private readonly CageRepository _cageRepository;
    private readonly ReservationRepository _reservationRepository;
    private readonly OwnerRepository _ownerRepository;

    private DateTimePicker _asOfDatePicker = null!;
    private DataGridView _grid = null!;

    public MainForm(BirdRepository birdRepository, CageRepository cageRepository, ReservationRepository reservationRepository, OwnerRepository ownerRepository)
    {
        _birdRepository = birdRepository;
        _cageRepository = cageRepository;
        _reservationRepository = reservationRepository;
        _ownerRepository = ownerRepository;

        BuildUi();
        RefreshGrid();
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

        var dateLabel = new Label { Text = "表示基準日", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(20, 10, 4, 0) };
        _asOfDatePicker = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Value = DateTime.Today };
        _asOfDatePicker.ValueChanged += (_, _) => RefreshGrid();

        var refreshButton = new Button { Text = "更新", Width = 80, Height = 36, Margin = new Padding(20, 0, 0, 0) };
        refreshButton.Click += (_, _) => RefreshGrid();

        topPanel.Controls.Add(ownerButton);
        topPanel.Controls.Add(birdButton);
        topPanel.Controls.Add(cageButton);
        topPanel.Controls.Add(reservationButton);
        topPanel.Controls.Add(dateLabel);
        topPanel.Controls.Add(_asOfDatePicker);
        topPanel.Controls.Add(refreshButton);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowTemplate = { Height = 32 },
        };
        _grid.Columns.Add("Name", "籠");
        _grid.Columns.Add("Capacity", "定員");
        _grid.Columns.Add("Occupied", "在籠数");
        _grid.Columns.Add("Remaining", "空き");
        _grid.Columns.Add("Birds", "在籠中の鳥");
        _grid.Columns.Add("Status", "状態");
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
                OpenReservationForm();
        };

        Controls.Add(_grid);
        Controls.Add(topPanel);
    }

    private void RefreshGrid()
    {
        var asOf = _asOfDatePicker.Value.Date;
        var cages = _cageRepository.GetAll();
        var activeReservations = _reservationRepository.GetActiveOn(asOf);

        _grid.Rows.Clear();
        foreach (var cage in cages)
        {
            var occupants = activeReservations.Where(r => r.CageId == cage.Id).ToList();
            var occupiedCount = occupants.Count;
            // 鳥が1羽でもいる籠は、定員に関わらず「空きなし」として扱う（同じ籠に別の予約の鳥を混在させないため）
            var remaining = occupiedCount > 0 ? 0 : cage.Capacity;
            var birdNames = string.Join(", ", occupants.Select(o => o.BirdName));
            var status = occupiedCount > 0 ? "満室" : "空きあり";

            var rowIndex = _grid.Rows.Add(cage.Name, cage.Capacity, occupiedCount, remaining, birdNames, status);
            if (status == "満室")
                _grid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.MistyRose;
        }
    }

    private void OpenOwnerList()
    {
        using var form = new OwnerListForm(_ownerRepository, _birdRepository);
        form.ShowDialog(this);
        RefreshGrid();
    }

    private void OpenBirdList()
    {
        using var form = new BirdListForm(_birdRepository, _ownerRepository);
        form.ShowDialog(this);
        RefreshGrid();
    }

    private void OpenCageList()
    {
        using var form = new CageListForm(_cageRepository);
        form.ShowDialog(this);
        RefreshGrid();
    }

    private void OpenReservationForm()
    {
        using var form = new ReservationForm(_birdRepository, _cageRepository, _reservationRepository, _ownerRepository);
        form.ShowDialog(this);
        RefreshGrid();
    }
}
