using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class BirdBulkImportForm : Form
{
    private readonly BirdRepository _birdRepository;
    private readonly OwnerRepository _ownerRepository;
    private readonly SpeciesRepository _speciesRepository;

    private TextBox _inputBox = null!;
    private DataGridView _previewGrid = null!;
    private Label _summaryLabel = null!;
    private List<ParsedRow> _parsedRows = new();

    private record ParsedRow(string Species, string Name, string Owner, bool CanPair, string PairName, string? Error);

    public BirdBulkImportForm(BirdRepository birdRepository, OwnerRepository ownerRepository, SpeciesRepository speciesRepository)
    {
        _birdRepository = birdRepository;
        _ownerRepository = ownerRepository;
        _speciesRepository = speciesRepository;
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "鳥の一括登録";
        Width = 860;
        Height = 660;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Yu Gothic UI", 10F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var instructionLabel = new Label
        {
            Text = "Excelなどから「種類・名前・飼い主・ペア・ペア名」の順で1行1羽になるようにコピーして貼り付けてください（タブ区切り・カンマ区切りどちらも可）。\n" +
                   "「ペア」は他の鳥と同じ籠に入れてよいかで、「可」または「不可」を入力します（空欄の場合はペア名の有無で判断します）。ペア可の場合は、同じ籠に入れる鳥同士に同じ「ペア名」を付けてください。\n" +
                   "まだ登録されていない種類・飼い主は自動的に新規登録されます。型は中小型、性別は不明、生年月日は不明として登録されるので、必要に応じて後から鳥の編集画面で修正してください。\n" +
                   "名前と飼い主が同じ鳥が既に登録済みの場合は二重登録せず、ペアが不可から可に変わる場合だけ既存の鳥を更新します。",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(810, 0),
        };
        root.Controls.Add(instructionLabel, 0, 0);

        _inputBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
        root.Controls.Add(_inputBox, 0, 1);

        var parseButton = new Button { Text = "解析してプレビュー", Width = 160, Height = 32, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6) };
        parseButton.Click += (_, _) => ParseAndPreview();
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
        _previewGrid.Columns.Add("Species", "種類");
        _previewGrid.Columns.Add("Name", "名前");
        _previewGrid.Columns.Add("Owner", "飼い主");
        _previewGrid.Columns.Add("CanPair", "ペア");
        _previewGrid.Columns.Add("PairName", "ペア名");
        _previewGrid.Columns.Add("Status", "状態");
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

    private void ParseAndPreview()
    {
        _parsedRows.Clear();
        _previewGrid.Rows.Clear();

        var existingSpeciesNames = new HashSet<string>(_speciesRepository.GetAll().Select(s => s.Name), StringComparer.Ordinal);
        var existingOwnerNames = new HashSet<string>(_ownerRepository.GetAll().Select(o => o.Name), StringComparer.Ordinal);
        var existingBirds = _birdRepository.GetAll();

        var lines = _inputBox.Text.Replace("\r\n", "\n").Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var parts = (line.Contains('\t') ? line.Split('\t') : line.Split(',')).Select(p => p.Trim()).ToArray();
            string Get(int i) => i < parts.Length ? parts[i] : "";

            var species = Get(0);
            var name = Get(1);
            var owner = Get(2);
            var pairText = Get(3);
            var pairName = Get(4);

            if (species == "種類" && name == "名前") continue; // ヘッダー行らしき行はスキップ

            var row = BuildRow(species, name, owner, pairText, pairName);
            _parsedRows.Add(row);

            var status = BuildStatus(row, existingBirds, existingSpeciesNames, existingOwnerNames);

            var rowIndex = _previewGrid.Rows.Add(
                species, name, owner,
                row.Error is null ? (row.CanPair ? "可" : "不可") : pairText,
                row.CanPair ? row.PairName : "X",
                status);
            if (row.Error is not null)
                _previewGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.MistyRose;
        }

        var validCount = _parsedRows.Count(r => r.Error is null);
        var errorCount = _parsedRows.Count - validCount;
        _summaryLabel.Text = $"{_parsedRows.Count}件中 登録可能{validCount}件 / エラー{errorCount}件";
    }

    private static ParsedRow BuildRow(string species, string name, string owner, string pairText, string pairName)
    {
        if (species.Length == 0)
            return new ParsedRow(species, name, owner, false, pairName, "種類が空です");
        if (name.Length == 0)
            return new ParsedRow(species, name, owner, false, pairName, "名前が空です");
        if (owner.Length == 0)
            return new ParsedRow(species, name, owner, false, pairName, "飼い主が空です");

        if (!TryParsePairFlag(pairText, pairName, out var canPair))
            return new ParsedRow(species, name, owner, false, pairName, "ペアの欄は「可」または「不可」で入力してください");

        if (canPair && pairName.Length == 0)
            return new ParsedRow(species, name, owner, true, pairName, "ペア可の場合はペア名を入力してください");

        return new ParsedRow(species, name, owner, canPair, canPair ? pairName : "", null);
    }

    private static bool TryParsePairFlag(string text, string pairName, out bool canPair)
    {
        // 空欄のときはペア名が入っていれば「可」とみなす
        if (text.Length == 0)
        {
            canPair = pairName.Length > 0;
            return true;
        }

        switch (text)
        {
            case "可" or "○" or "〇" or "◯" or "はい" or "OK" or "ok" or "o" or "O" or "1":
                canPair = true;
                return true;
            case "不可" or "X" or "x" or "×" or "いいえ" or "NG" or "ng" or "-" or "0":
                canPair = false;
                return true;
            default:
                canPair = false;
                return false;
        }
    }

    private static string BuildStatus(ParsedRow row, List<Bird> existingBirds, HashSet<string> speciesNames, HashSet<string> ownerNames)
    {
        if (row.Error is not null)
            return "エラー: " + row.Error;

        var existingBird = FindExistingBird(existingBirds, row.Name, row.Owner);
        if (existingBird is not null)
        {
            if (row.CanPair && (!existingBird.CanPair || existingBird.PairName != row.PairName))
                return $"既に登録済み → ペアを可（{row.PairName}）に更新";
            return "既に登録済み（変更なし）";
        }

        var notes = new List<string>();
        if (!speciesNames.Contains(row.Species)) { notes.Add("種類を新規登録"); speciesNames.Add(row.Species); }
        if (!ownerNames.Contains(row.Owner)) { notes.Add("飼い主を新規登録"); ownerNames.Add(row.Owner); }
        return notes.Count == 0 ? "新規登録" : "新規登録 / " + string.Join(" / ", notes);
    }

    // 名前と飼い主が一致する既存の鳥を探す（同じ鳥を二重に作らないため）
    private static Bird? FindExistingBird(List<Bird> birds, string name, string owner) =>
        birds.FirstOrDefault(b => b.Name == name && b.OwnerName == owner);

    private void RegisterAll()
    {
        if (_parsedRows.Count == 0)
        {
            MessageBox.Show("先に「解析してプレビュー」を実行してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var validRows = _parsedRows.Where(r => r.Error is null).ToList();
        if (validRows.Count == 0)
        {
            MessageBox.Show("登録できる行がありません。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show($"{validRows.Count}件を登録します。よろしいですか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        var existingSpeciesNames = new HashSet<string>(_speciesRepository.GetAll().Select(s => s.Name), StringComparer.Ordinal);
        var ownersByName = _ownerRepository.GetAll().ToDictionary(o => o.Name, o => o.Id, StringComparer.Ordinal);
        var existingBirds = _birdRepository.GetAll();

        var addedCount = 0;
        var updatedCount = 0;

        foreach (var row in validRows)
        {
            if (existingSpeciesNames.Add(row.Species))
                _speciesRepository.Insert(new Species { Name = row.Species });

            if (!ownersByName.TryGetValue(row.Owner, out var ownerId))
            {
                ownerId = _ownerRepository.Insert(new Owner { Name = row.Owner });
                ownersByName[row.Owner] = ownerId;
            }

            var existingBird = FindExistingBird(existingBirds, row.Name, row.Owner);
            if (existingBird is not null)
            {
                // 既に登録済みの鳥は二重登録しない。ペアが不可から可になる場合だけ更新する
                if (row.CanPair && (!existingBird.CanPair || existingBird.PairName != row.PairName))
                {
                    existingBird.CanPair = true;
                    existingBird.PairName = row.PairName;
                    _birdRepository.Update(existingBird);
                    updatedCount++;
                }
                continue;
            }

            var newBird = new Bird
            {
                Species = row.Species,
                Name = row.Name,
                Size = BirdSize.中小型,
                Gender = BirdGender.不明,
                OwnerId = ownerId,
                CanPair = row.CanPair,
                PairName = row.PairName,
            };
            newBird.Id = _birdRepository.Insert(newBird);
            newBird.OwnerName = row.Owner;
            existingBirds.Add(newBird);
            addedCount++;
        }

        MessageBox.Show($"新規登録{addedCount}件 / 既存の鳥を更新{updatedCount}件 で登録しました。", "登録完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
