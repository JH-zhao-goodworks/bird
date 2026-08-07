using System.Globalization;
using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class ReservationBulkImportForm : Form
{
    private readonly BirdRepository _birdRepository;
    private readonly OwnerRepository _ownerRepository;
    private readonly SpeciesRepository _speciesRepository;
    private readonly CageRepository _cageRepository;
    private readonly ReservationRepository _reservationRepository;

    private TextBox _inputBox = null!;
    private DataGridView _previewGrid = null!;
    private Label _summaryLabel = null!;

    private List<GroupPlan> _plannedGroups = new();

    private record ParsedRow(int LineNumber, string Species, string Name, string Owner, string Group, DateTime Start, DateTime? End, string? Error);

    private record GroupPlan(List<ParsedRow> Members, Cage? AssignedCage, string Status);

    public ReservationBulkImportForm(
        BirdRepository birdRepository,
        OwnerRepository ownerRepository,
        SpeciesRepository speciesRepository,
        CageRepository cageRepository,
        ReservationRepository reservationRepository)
    {
        _birdRepository = birdRepository;
        _ownerRepository = ownerRepository;
        _speciesRepository = speciesRepository;
        _cageRepository = cageRepository;
        _reservationRepository = reservationRepository;
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "予約の一括登録（自動籠配属）";
        Width = 860;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var instructionLabel = new Label
        {
            Text = "1行1羽で「種類, 名前, 飼い主, グループ, 開始日, 終了日」の順に貼り付けてください（タブ区切り・カンマ区切り対応）。\n" +
                   "同じ籠に入れたい鳥には同じグループ名を付けてください（グループ名・開始日・終了日が一致する行だけ同じ籠にまとまります）。単独の籠でよい場合はグループ欄を空欄にしてください。\n" +
                   "終了日を空欄または「無期限」にすると、経営者の鳥のような退室日未定の予約になります。空いている籠へ自動的に割り当てられます。",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(810, 0),
        };
        root.Controls.Add(instructionLabel, 0, 0);

        _inputBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
        root.Controls.Add(_inputBox, 0, 1);

        var parseButton = new Button { Text = "解析して配属プレビュー", Width = 180, Height = 32, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6) };
        parseButton.Click += (_, _) => ParseAndPlan();
        root.Controls.Add(parseButton, 0, 2);

        _previewGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowTemplate = { Height = 28 },
        };
        _previewGrid.Columns.Add("Group", "グループ");
        _previewGrid.Columns.Add("Members", "鳥（種類・飼い主）");
        _previewGrid.Columns.Add("Start", "開始日");
        _previewGrid.Columns.Add("End", "終了日");
        _previewGrid.Columns.Add("Cage", "配属先");
        foreach (DataGridViewColumn col in _previewGrid.Columns)
            col.SortMode = DataGridViewColumnSortMode.NotSortable;
        root.Controls.Add(_previewGrid, 0, 3);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancelButton = new Button { Text = "閉じる", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var registerButton = new Button { Text = "この内容で登録", Width = 140, Height = 32 };
        registerButton.Click += (_, _) => RegisterAll();
        _summaryLabel = new Label { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(10, 10, 10, 0) };
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(registerButton);
        buttonPanel.Controls.Add(_summaryLabel);
        CancelButton = cancelButton;
        root.Controls.Add(buttonPanel, 0, 4);

        Controls.Add(root);
    }

    private void ParseAndPlan()
    {
        _previewGrid.Rows.Clear();
        _plannedGroups.Clear();

        var validRows = new List<ParsedRow>();
        var errorRows = new List<ParsedRow>();

        var lines = _inputBox.Text.Replace("\r\n", "\n").Split('\n');
        var lineNumber = 0;
        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var parts = (line.Contains('\t') ? line.Split('\t') : line.Split(',')).Select(p => p.Trim()).ToArray();
            string Get(int i) => i < parts.Length ? parts[i] : "";

            var species = Get(0);
            var name = Get(1);
            var owner = Get(2);
            var group = Get(3);
            var startText = Get(4);
            var endText = Get(5);

            if (species == "種類" && name == "名前") continue; // ヘッダー行らしき行はスキップ

            var row = BuildRow(lineNumber, species, name, owner, group, startText, endText);
            if (row.Error is null) validRows.Add(row);
            else errorRows.Add(row);
        }

        foreach (var group in PlanGroups(validRows))
            _plannedGroups.Add(group);

        foreach (var group in _plannedGroups)
        {
            var members = string.Join("、", group.Members.Select(m => $"{m.Name}（{m.Species}・{m.Owner}）"));
            var start = group.Members[0].Start.ToString("yyyy/MM/dd");
            var end = group.Members[0].End?.ToString("yyyy/MM/dd") ?? "無期限";
            var groupLabel = group.Members[0].Group.Length > 0 ? group.Members[0].Group : "(単独)";
            var rowIndex = _previewGrid.Rows.Add(groupLabel, members, start, end, group.Status);
            if (group.AssignedCage is null)
                _previewGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.MistyRose;
        }

        foreach (var errorRow in errorRows)
        {
            var rowIndex = _previewGrid.Rows.Add("-", $"{errorRow.LineNumber}行目: {errorRow.Name}", "-", "-", "エラー: " + errorRow.Error);
            _previewGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.MistyRose;
        }

        var assignedCount = _plannedGroups.Count(g => g.AssignedCage is not null);
        var totalBirds = _plannedGroups.Sum(g => g.Members.Count);
        _summaryLabel.Text = $"{_plannedGroups.Count}グループ（{totalBirds}羽）中 配属可能{assignedCount}件 / 配属不可{_plannedGroups.Count - assignedCount}件 / 行エラー{errorRows.Count}件";
    }

    private static ParsedRow BuildRow(int lineNumber, string species, string name, string owner, string group, string startText, string endText)
    {
        if (species.Length == 0)
            return new ParsedRow(lineNumber, species, name, owner, group, default, null, "種類が空です");
        if (name.Length == 0)
            return new ParsedRow(lineNumber, species, name, owner, group, default, null, "名前が空です");
        if (owner.Length == 0)
            return new ParsedRow(lineNumber, species, name, owner, group, default, null, "飼い主が空です");
        if (!TryParseDate(startText, out var start))
            return new ParsedRow(lineNumber, species, name, owner, group, default, null, "開始日が読み取れません");

        DateTime? end;
        if (endText.Length == 0 || endText is "無期限" or "むきげん")
        {
            end = null;
        }
        else if (TryParseDate(endText, out var endValue))
        {
            end = endValue;
        }
        else
        {
            return new ParsedRow(lineNumber, species, name, owner, group, start, null, "終了日が読み取れません");
        }

        if (end is not null && end.Value < start)
            return new ParsedRow(lineNumber, species, name, owner, group, start, end, "終了日が開始日より前です");

        return new ParsedRow(lineNumber, species, name, owner, group, start, end, null);
    }

    private static bool TryParseDate(string text, out DateTime value)
    {
        string[] formats = ["yyyy/M/d", "yyyy-M-d", "yyyy/MM/dd", "yyyy-MM-dd"];
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            return true;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private List<GroupPlan> PlanGroups(List<ParsedRow> validRows)
    {
        // グループ名・開始日・終了日が一致する行をひとまとめにする。グループ名が空欄の行は1羽ずつ単独グループになる。
        var used = new bool[validRows.Count];
        var rawGroups = new List<(int FirstIndex, List<ParsedRow> Members)>();

        for (var i = 0; i < validRows.Count; i++)
        {
            if (used[i]) continue;
            var row = validRows[i];
            var members = new List<ParsedRow> { row };
            used[i] = true;

            if (row.Group.Length > 0)
            {
                for (var j = i + 1; j < validRows.Count; j++)
                {
                    if (used[j]) continue;
                    var other = validRows[j];
                    if (other.Group == row.Group && other.Start == row.Start && other.End == row.End)
                    {
                        members.Add(other);
                        used[j] = true;
                    }
                }
            }

            rawGroups.Add((i, members));
        }

        // 経営者籠・持ち込み籠は決まった鳥専用のため、自動配属の候補にしない
        var cages = _cageRepository.GetAll().Where(c => c.IsAutoAssignable).ToList();

        // 同じ鳥・同じ期間の既存予約は登録時に消して入れ直すので、空きとして扱う
        var replacedReservationIds = FindReplacedReservationIds(validRows);

        var provisional = new List<(int CageId, DateTime Start, DateTime? End)>();
        foreach (var cage in cages)
            foreach (var reservation in _reservationRepository.GetByCage(cage.Id))
                if (!replacedReservationIds.Contains(reservation.Id))
                    provisional.Add((cage.Id, reservation.StartDate, reservation.EndDate));

        var allBirds = _birdRepository.GetAll();
        var plans = new List<GroupPlan>();
        foreach (var (_, members) in rawGroups.OrderBy(g => g.FirstIndex))
        {
            var start = members[0].Start;
            var end = members[0].End;
            var size = members.Count;

            var cage = cages.FirstOrDefault(c =>
                size <= c.Capacity &&
                !provisional.Any(p => p.CageId == c.Id && Overlaps(p.Start, p.End, start, end)));

            var replaceNote = members.Any(m => HasSamePeriodReservation(allBirds, m)) ? "（既存予約を置き換え）" : "";

            if (cage is null)
            {
                plans.Add(new GroupPlan(members, null, "自動配属できませんでした（空き籠なし）"));
            }
            else
            {
                provisional.Add((cage.Id, start, end));
                plans.Add(new GroupPlan(members, cage, $"「{cage.Name}」に配属{replaceNote}"));
            }
        }

        return plans;
    }

    // 名前と飼い主が一致する既存の鳥を探す（一括登録で同じ鳥を二重に作らないため）
    private static Bird? FindExistingBird(List<Bird> birds, string name, string owner) =>
        birds.FirstOrDefault(b => b.Name == name && b.OwnerName == owner);

    private bool HasSamePeriodReservation(List<Bird> birds, ParsedRow row)
    {
        var bird = FindExistingBird(birds, row.Name, row.Owner);
        if (bird is null) return false;
        return _reservationRepository.GetByBird(bird.Id)
            .Any(r => r.StartDate == row.Start && r.EndDate == row.End);
    }

    // 同じ鳥・同じ期間で既に入っている予約（登録時に削除して入れ直す対象）を集める
    private HashSet<int> FindReplacedReservationIds(List<ParsedRow> rows)
    {
        var result = new HashSet<int>();
        var birds = _birdRepository.GetAll();

        foreach (var row in rows)
        {
            var bird = FindExistingBird(birds, row.Name, row.Owner);
            if (bird is null) continue;

            foreach (var reservation in _reservationRepository.GetByBird(bird.Id))
                if (reservation.StartDate == row.Start && reservation.EndDate == row.End)
                    result.Add(reservation.Id);
        }

        return result;
    }

    private static bool Overlaps(DateTime aStart, DateTime? aEnd, DateTime bStart, DateTime? bEnd)
    {
        var aEndValue = aEnd ?? DateTime.MaxValue;
        var bEndValue = bEnd ?? DateTime.MaxValue;
        return aStart <= bEndValue && bStart <= aEndValue;
    }

    private void RegisterAll()
    {
        if (_plannedGroups.Count == 0)
        {
            MessageBox.Show("先に「解析して配属プレビュー」を実行してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var assignable = _plannedGroups.Where(g => g.AssignedCage is not null).ToList();
        if (assignable.Count == 0)
        {
            MessageBox.Show("配属できるグループがありません。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var totalBirds = assignable.Sum(g => g.Members.Count);
        var confirm = MessageBox.Show($"{assignable.Count}グループ（{totalBirds}羽）を登録します。よろしいですか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        var existingSpeciesNames = new HashSet<string>(_speciesRepository.GetAll().Select(s => s.Name), StringComparer.Ordinal);
        var ownersByName = _ownerRepository.GetAll().ToDictionary(o => o.Name, o => o.Id, StringComparer.Ordinal);
        var existingBirds = _birdRepository.GetAll();
        var replacedCount = 0;

        foreach (var group in assignable)
        {
            foreach (var member in group.Members)
            {
                if (existingSpeciesNames.Add(member.Species))
                    _speciesRepository.Insert(new Species { Name = member.Species });

                if (!ownersByName.TryGetValue(member.Owner, out var ownerId))
                {
                    ownerId = _ownerRepository.Insert(new Owner { Name = member.Owner });
                    ownersByName[member.Owner] = ownerId;
                }

                // 同じ名前・同じ飼い主の鳥が既にいればそれを使う（二重登録を防ぐ）
                var existingBird = FindExistingBird(existingBirds, member.Name, member.Owner);
                int birdId;
                if (existingBird is not null)
                {
                    birdId = existingBird.Id;

                    // 同じ期間の予約が既にあれば、消してから登録し直す
                    foreach (var reservation in _reservationRepository.GetByBird(birdId))
                    {
                        if (reservation.StartDate == member.Start && reservation.EndDate == member.End)
                        {
                            _reservationRepository.Delete(reservation.Id);
                            replacedCount++;
                        }
                    }
                }
                else
                {
                    birdId = _birdRepository.Insert(new Bird
                    {
                        Species = member.Species,
                        Name = member.Name,
                        Size = BirdSize.中小型,
                        Gender = BirdGender.不明,
                        OwnerId = ownerId,
                        // グループ名をそのままペア名として引き継ぐ（同じ籠に入れてよい鳥の目印になる）
                        CanPair = member.Group.Length > 0,
                        PairName = member.Group,
                    });
                    existingBirds.Add(new Bird { Id = birdId, Name = member.Name, OwnerName = member.Owner });
                }

                _reservationRepository.Insert(new Reservation
                {
                    BirdId = birdId,
                    CageId = group.AssignedCage!.Id,
                    StartDate = member.Start,
                    EndDate = member.End,
                });
            }
        }

        var replacedText = replacedCount > 0 ? $"\n（うち{replacedCount}件は同じ鳥・同じ期間の既存予約を置き換えました）" : "";
        MessageBox.Show($"{assignable.Count}グループ（{totalBirds}羽）を登録しました。{replacedText}", "登録完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
