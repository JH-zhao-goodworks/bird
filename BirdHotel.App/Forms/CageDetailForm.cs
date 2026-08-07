using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class CageDetailForm : Form
{
    private readonly Cage _cage;
    private readonly BirdRepository _birdRepository;
    private readonly CageRepository _cageRepository;
    private readonly ReservationRepository _reservationRepository;
    private readonly OwnerRepository _ownerRepository;

    private Label _headerLabel = null!;
    private DataGridView _grid = null!;
    private List<Reservation> _reservations = new();

    public CageDetailForm(
        Cage cage,
        BirdRepository birdRepository,
        CageRepository cageRepository,
        ReservationRepository reservationRepository,
        OwnerRepository ownerRepository)
    {
        _cage = cage;
        _birdRepository = birdRepository;
        _cageRepository = cageRepository;
        _reservationRepository = reservationRepository;
        _ownerRepository = ownerRepository;

        BuildUi();
        RefreshGrid();
    }

    private void BuildUi()
    {
        Text = $"{_cage.Name} の詳細";
        Width = 900;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        _headerLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(12, 6, 0, 0),
            Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold),
        };

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
        var bookButton = new Button { Text = "鳥を登録", Width = 110, Height = 32 };
        bookButton.Click += (_, _) => OpenBooking();
        var moveButton = new Button { Text = "移動・交換", Width = 110, Height = 32 };
        moveButton.Click += (_, _) => OpenMove();
        var clearButton = new Button { Text = "クリア", Width = 100, Height = 32 };
        clearButton.Click += (_, _) => ClearCage();
        var closeButton = new Button { Text = "閉じる", Width = 90, Height = 32, DialogResult = DialogResult.Cancel, Margin = new Padding(20, 3, 3, 3) };
        buttonPanel.Controls.Add(bookButton);
        buttonPanel.Controls.Add(moveButton);
        buttonPanel.Controls.Add(clearButton);
        buttonPanel.Controls.Add(closeButton);
        CancelButton = closeButton;

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
        _grid.Columns.Add("Name", "名前");
        _grid.Columns.Add("Species", "種類");
        _grid.Columns.Add("Owner", "飼い主");
        _grid.Columns.Add("Pair", "ペア名");
        _grid.Columns.Add("Start", "開始日");
        _grid.Columns.Add("End", "終了日");
        _grid.Columns.Add("Total", "合計");
        foreach (DataGridViewColumn col in _grid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable;

        Controls.Add(_grid);
        Controls.Add(buttonPanel);
        Controls.Add(_headerLabel);
    }

    private void RefreshGrid()
    {
        _reservations = _reservationRepository.GetByCage(_cage.Id)
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.BirdName)
            .ToList();
        var birdsById = _birdRepository.GetAll().ToDictionary(b => b.Id);

        var maxConcurrent = 0;
        foreach (var r in _reservations)
        {
            var concurrent = _reservations.Count(other => r.OverlapsWith(other.StartDate, other.EndDate));
            maxConcurrent = Math.Max(maxConcurrent, concurrent);
        }

        var typeText = _cage.Type == CageType.通常籠 ? "" : $"　[{_cage.Type}]";
        var specialText = maxConcurrent > _cage.Capacity ? $"　（特{maxConcurrent}）" : "";
        _headerLabel.Text = $"{_cage.Name}　定員{_cage.Capacity}{typeText}{specialText}　予約{_reservations.Count}件";

        _grid.Rows.Clear();
        foreach (var r in _reservations)
        {
            birdsById.TryGetValue(r.BirdId, out var bird);

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

            _grid.Rows.Add(
                r.BirdName,
                bird?.Species ?? "",
                r.OwnerName,
                bird is not null && bird.CanPair && bird.PairName.Length > 0 ? bird.PairName : "X",
                startText,
                endText,
                totalText);
        }
    }

    private void OpenBooking()
    {
        using var form = new CageBookingForm(_cage, _birdRepository, _reservationRepository, _ownerRepository);
        form.ShowDialog(this);
        RefreshGrid();
    }

    private void OpenMove()
    {
        if (_reservations.Count == 0)
        {
            MessageBox.Show($"「{_cage.Name}」には移動できる鳥がいません。", "移動", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new CageMoveForm(_cage, _cageRepository, _reservationRepository);
        form.ShowDialog(this);
        RefreshGrid();
    }

    private void ClearCage()
    {
        if (_reservations.Count == 0)
        {
            MessageBox.Show($"「{_cage.Name}」には予約がありません。", "クリア", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"「{_cage.Name}」の予約を{_reservations.Count}件すべて取り消します。よろしいですか？（元に戻せません）",
            "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        foreach (var reservation in _reservations)
            _reservationRepository.Delete(reservation.Id);

        RefreshGrid();
    }
}
