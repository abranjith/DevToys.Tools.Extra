using DevToys.Api;
using DevToys.UrlParser.Helpers;
using Microsoft.Extensions.Logging;
using System.ComponentModel.Composition;

namespace DevToys.UrlParser;

[Export(typeof(IGuiTool))]
[Name("UrlParser")]
[ToolDisplayInformation(
    IconFontName = "DevToys-Tools-Icons",
    IconGlyph = '\u0121',
    GroupName = "Converters",
    ResourceManagerAssemblyIdentifier = nameof(DevToysToolsExtraUrlParserResourceManagerAssemblyIdentifier),
    ResourceManagerBaseName = "DevToys.UrlParser.UrlParser",
    ShortDisplayTitleResourceName = nameof(UrlParser.ShortDisplayTitle),
    LongDisplayTitleResourceName = nameof(UrlParser.LongDisplayTitle),
    DescriptionResourceName = nameof(UrlParser.Description),
    SearchKeywordsResourceName = nameof(UrlParser.SearchKeywords),
    AccessibleNameResourceName = nameof(UrlParser.AccessibleName))]
internal sealed partial class UrlParserGuiTool : IGuiTool, IDisposable
{
    private readonly DisposableSemaphore _semaphore = new();
    private readonly ILogger _logger;
    private readonly ISettingsProvider _settingsProvider;
    private CancellationTokenSource? _cancellationTokenSource;

    #region :: UI Components ::

    //input
    private readonly IUISingleLineTextInput _urlInputTextArea = GUI.SingleLineTextInput("urlparser-url-text-area");

    //output
    private readonly IUISingleLineTextInput _schemaOutputArea = GUI.SingleLineTextInput("urlparser-schema-text-area");
    private readonly IUINumberInput _portOutputArea = GUI.NumberInput("urlparser-port-text-area");
    private readonly IUISingleLineTextInput _hostOutputArea = GUI.SingleLineTextInput("urlparser-host-text-area");
    private readonly IUISingleLineTextInput _queryPathOutputArea = GUI.SingleLineTextInput("urlparser-querypath-text-area");
    private readonly IUIDataGrid _queryStringDataGrid = GUI.DataGrid("urlparser-host-querystring-data-grid");
    private readonly IUIMultiLineTextInput _ipv4TextArea = GUI.MultiLineTextInput("urlparser-ipv4-text-area");
    private readonly IUIMultiLineTextInput _ipv6TextArea = GUI.MultiLineTextInput("urlparser-ipv6-text-area");

    private readonly IUIInfoBar _infoBar = GUI.InfoBar("urlparser-error-banner");

    #endregion


    [ImportingConstructor]
    public UrlParserGuiTool(ISettingsProvider settingsProvider)
    {
        _logger = this.Log();
        _settingsProvider = settingsProvider;
    }

    internal Task? WorkTask { get; private set; }

    public void OnDataReceived(string dataTypeName, object? parsedData)
    {
        throw new NotImplementedException();
    }

    #region :: View ::

    public UIToolView View
    => new(
        isScrollable: true,
        GUI.Grid("urlparser-main-grid")
            .RowLargeSpacing()
            .Rows(
                (MainGridRow.Banner, GUI.Auto),
                (MainGridRow.Url, new UIGridLength(2, UIGridUnitType.Fraction)),
                (MainGridRow.Output, new UIGridLength(10, UIGridUnitType.Fraction))
            )
            .Columns(
                (MainGridColumn.Content, GUI.Auto)
            )
        .Cells(
            GUI.Cell(
                MainGridRow.Banner,
                MainGridColumn.Content,
                _infoBar
                    .Error()
                    .Close()
                    .Hide()
            ),
            GUI.Cell(
                MainGridRow.Url,
                MainGridColumn.Content,
                _urlInputTextArea
                    .OnTextChanged(OnUrlChanged)
                    .CanCopyWhenEditable()
            ),
            GUI.Cell(
                MainGridRow.Output,
                MainGridColumn.Content,
                GUI.Stack()
                    .Vertical()
                    .SmallSpacing()
                    .WithChildren(
                        HostStack(),
                        _queryPathOutputArea
                            .Title("Path")
                            .ReadOnly()
                            .HideCommandBar(),
                        _queryStringDataGrid
                            .Title("Query")
                            .Extendable()
                            .AllowSelectItem()
                            .Hide(),
                        IPStack()

                    )
                )
            )
    );

    #endregion

    private void OnUrlChanged(string url)
    {
        StartSend(url);
    }

    private void StartSend(string url)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _infoBar.Close();
        _infoBar.Hide();
        _cancellationTokenSource = new CancellationTokenSource();

        WorkTask = SendAsync(url, _cancellationTokenSource.Token);
    }

    private async Task SendAsync(string url, CancellationToken cancellationToken)
    {
        using (await _semaphore.WaitAsync(cancellationToken))
        {
            await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(cancellationToken);

            ResultInfo<UrlParserResponse> formatResult = await UrlParserHelper.ParseAsync(
                url,
                _logger,
                cancellationToken);

            if (formatResult.HasSucceeded)
            {
                _infoBar.Close();
                _infoBar.Hide();
                _schemaOutputArea.Text(formatResult.Data.Schema ?? string.Empty);
                _portOutputArea.Text(formatResult.Data.Port?.ToString() ?? string.Empty);
                _hostOutputArea.Text(formatResult.Data.HostName ?? string.Empty);
                _queryPathOutputArea.Text(formatResult.Data.UrlPath ?? string.Empty);
                SetDataGridData(formatResult.Data.QueryString);
                _ipv4TextArea.Text(string.Join(Environment.NewLine, formatResult.Data.IPv4));
                _ipv6TextArea.Text(string.Join(Environment.NewLine, formatResult.Data.IPv6));
            }
            else
            {
                _infoBar.Title(formatResult.ErrorMessage);
                _infoBar.Open();
                _infoBar.Show();
            }
        }
    }

    private void SetDataGridData(IList<KeyValuePair<string,string>> data)
    {
        if(!data.Any())
        {
            _queryStringDataGrid.Hide();
            return;
        }
        IUIDataGridRow[] rows = data.Select(kv => GUI.Row(null, kv.Key, kv.Value)).ToArray();
        _queryStringDataGrid.WithColumns("Key", "Value");
        _queryStringDataGrid.WithRows(rows);
        _queryStringDataGrid.Show();
    }


    #region :: Schema / Port / Host Stack ::

    private IUIStack HostStack()
    {
        return
            GUI.Stack()
            .Vertical()
            .WithChildren(
                GUI.Grid("urlparser-host-data-grid")
                .RowSmallSpacing()
                .ColumnSmallSpacing()
                .Rows(
                    (OutputGridRow.Content, GUI.Auto)
                )
                .Columns(
                    (HostGridColumn.Schema, new UIGridLength(2, UIGridUnitType.Fraction)),
                    (HostGridColumn.Port, new UIGridLength(1, UIGridUnitType.Fraction)),
                    (HostGridColumn.Host, new UIGridLength(7, UIGridUnitType.Fraction))
                )
                .Cells(
                    GUI.Cell(
                        OutputGridRow.Content,
                        HostGridColumn.Schema,
                        _schemaOutputArea
                            .Title("Schema")
                            .ReadOnly()
                            .HideCommandBar()
                    ),
                    GUI.Cell(
                        OutputGridRow.Content,
                        HostGridColumn.Port,
                        _portOutputArea
                            .Title("Port")
                            .ReadOnly()
                            .HideCommandBar()
                    ),
                    GUI.Cell(
                        OutputGridRow.Content,
                        HostGridColumn.Host,
                        _hostOutputArea
                            .Title("Host")
                            .ReadOnly()
                            .HideCommandBar()
                    )
                )
            );
    }

    #endregion


    #region :: IP Stack ::

    private IUIStack IPStack()
    {
        return
            GUI.Stack()
            .Vertical()
            .WithChildren(
                GUI.Grid("urlparser-ip-data-grid")
                .RowSmallSpacing()
                .ColumnSmallSpacing()
                .Rows(
                    (OutputGridRow.Content, GUI.Auto)
                )
                .Columns(
                    (IpGridColumn.IPV4, new UIGridLength(1, UIGridUnitType.Fraction)),
                    (IpGridColumn.IPV6, new UIGridLength(1, UIGridUnitType.Fraction))
                )
                .Cells(
                    GUI.Cell(
                        OutputGridRow.Content,
                        IpGridColumn.IPV4,
                        _ipv4TextArea
                            .Title("IPv4")
                            .ReadOnly()
                    ),
                    GUI.Cell(
                        OutputGridRow.Content,
                        IpGridColumn.IPV6,
                        _ipv6TextArea
                            .Title("IPv6")
                            .ReadOnly()
                    )
                )
            );
    }

    #endregion

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _semaphore.Dispose();
    }
}

#region :: Grids ::

internal enum MainGridColumn
{
    Content,
}

internal enum MainGridRow
{
    Banner,
    Url,
    Output
}

internal enum HostGridColumn
{
    Schema,
    Port,
    Host
}

internal enum IpGridColumn
{
    IPV4,
    IPV6,
}

internal enum OutputGridRow
{
    Content
}

#endregion
