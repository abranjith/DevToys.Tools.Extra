using DevToys.Api;
using System.ComponentModel.Composition;
using Microsoft.Extensions.Logging;
using DevToys.HmacSignature.Helpers;

namespace DevToys.HmacSignature;

[Export(typeof(IGuiTool))]
[Name("HmacSignature")]
[ToolDisplayInformation(
    IconFontName = "FluentSystemIcons",
    IconGlyph = '\uE8C9',
    GroupName = "Generators",
    ResourceManagerAssemblyIdentifier = nameof(DevToysToolsExtraHmacSignatureResourceManagerAssemblyIdentifier),
    ResourceManagerBaseName = "DevToys.HmacSignature.HmacSignature",
    ShortDisplayTitleResourceName = nameof(HmacSignature.ShortDisplayTitle),
    LongDisplayTitleResourceName = nameof(HmacSignature.LongDisplayTitle),
    DescriptionResourceName = nameof(HmacSignature.Description),
    SearchKeywordsResourceName = nameof(HmacSignature.SearchKeywords),
    AccessibleNameResourceName = nameof(HmacSignature.AccessibleName))]
internal sealed partial class HmacSignatureGuiTool : IGuiTool, IDisposable
{
    private readonly DisposableSemaphore _semaphore = new();
    private readonly ILogger _logger;
    private readonly ISettingsProvider _settingsProvider;
    private CancellationTokenSource? _cancellationTokenSource;
    internal Task? WorkTask { get; private set; }

    [ImportingConstructor]
    public HmacSignatureGuiTool(ISettingsProvider settingsProvider)
    {
        _logger = this.Log();
        _settingsProvider = settingsProvider;
        _infoBar.Close().Hide();
        InitializeHttpMethodsDropdown(_httpMethodsDropdown);
    }

    #region :: Settings ::

    private static readonly SettingDefinition<HashAlgorithmEnum> contentHashAlgorithmSetting
        = new(
            name: $"{nameof(HashAlgorithmEnum)}.{nameof(contentHashAlgorithmSetting)}",
            defaultValue: HashAlgorithmEnum.SHA256);

    private static readonly SettingDefinition<HashAlgorithmEnum> hmacHashAlgorithmSetting
        = new(
            name: $"{nameof(HashAlgorithmEnum)}.{nameof(hmacHashAlgorithmSetting)}",
            defaultValue: HashAlgorithmEnum.SHA256);

    #endregion


    #region :: UI Components ::

    //input
    private readonly IUISelectDropDownList _httpMethodsDropdown = GUI.SelectDropDownList("hmac-http-method-list");
    private readonly IUIPasswordInput _secret = GUI.PasswordInput("hmac-secret-input");
    private readonly IUISingleLineTextInput _requestUrl = GUI.SingleLineTextInput("hmac-request-url");
    private readonly IUIMultiLineTextInput _requestContent = GUI.MultiLineTextInput("hmac-request-content-input");
    private readonly IUIButton _generateButton = GUI.Button("hmac-generate-btn");

    //output
    private readonly IUIDataGrid _outputGrid = GUI.DataGrid("hmac-signature-data-grid"); 
    private readonly IUIInfoBar _infoBar = GUI.InfoBar("hmac-info-banner");

    #endregion


    #region :: View ::

    public UIToolView View
    => new(
        isScrollable: true,
        GUI.Grid("hmac-main-grid")
            .RowLargeSpacing()
            .Rows(
                (MainGridRow.Banner, GUI.Auto),
                (MainGridRow.Options, GUI.Auto),
                (MainGridRow.Secret, new UIGridLength(1, UIGridUnitType.Fraction)),
                (MainGridRow.Url, new UIGridLength(1, UIGridUnitType.Fraction)),
                (MainGridRow.Content, new UIGridLength(3, UIGridUnitType.Fraction)),
                (MainGridRow.Output, new UIGridLength(5, UIGridUnitType.Fraction))
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
                    MainGridRow.Options,
                    MainGridColumn.Content,
                    GUI.Stack()
                        .Vertical()
                        .WithChildren(
                            GUI.SettingGroup("hmac-setting-group")
                                .Icon("FluentSystemIcons", '\uec9e')
                                .Title("Setting")
                                .Description("Select settings")
                                .WithSettings(
                                    GUI.Setting("hmac-algorithm-setting")
                                        .Icon("FluentSystemIcons", '\uF1EE')
                                        .Title("HMAC Hash Algorithm")
                                        .Handle(
                                                _settingsProvider,
                                                hmacHashAlgorithmSetting,
                                                onOptionSelected: OnHmacHashAlgorithmChanged,
                                                GUI.Item(HashAlgorithmEnum.SHA1),
                                                GUI.Item(HashAlgorithmEnum.SHA256)
                                            ),
                                    GUI.Setting("hmac-content-algorithm-setting")
                                        .Icon("FluentSystemIcons", '\uF1EE')
                                        .Title("Content Hash Algorithm")
                                        .Handle(
                                                _settingsProvider,
                                                contentHashAlgorithmSetting,
                                                onOptionSelected: OnContentHashAlgorithmChanged,
                                                GUI.Item(HashAlgorithmEnum.SHA1),
                                                GUI.Item(HashAlgorithmEnum.SHA256)
                                            )
                                )
                        )
                ),
            GUI.Cell(
                MainGridRow.Secret,
                MainGridColumn.Content,
                _secret
                    .Title("Secret")
                    .CanCopyWhenEditable()
                ),
            GUI.Cell(
                MainGridRow.Url,
                MainGridColumn.Content,
                UrlStack()
                ),
            GUI.Cell(
                MainGridRow.Content,
                MainGridColumn.Content,
                _requestContent
                    .Title("Request Content")
                ),
            GUI.Cell(
                MainGridRow.Output,
                MainGridColumn.Content,
                _outputGrid
                    .Title("HMAC Signature")
                    .WithColumns(
                        "Header",
                        "Value"
                    )
                    .CommandBarExtraContent(
                        _generateButton
                            .AccentAppearance()
                            .Text("Generate")
                            .OnClick(OnGenerate)
                        )
                )
            )
    );

    #endregion

    private void OnGenerate()
        => OnGenerateRequest();

    private void OnGenerateRequest()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _infoBar.Close().Hide();
        _cancellationTokenSource = new CancellationTokenSource();

        WorkTask = OnGenerateRequestAsync(_cancellationTokenSource.Token);
    }

    private async Task OnGenerateRequestAsync(CancellationToken cancellationToken)
    {
        using (await _semaphore.WaitAsync(cancellationToken))
        {
            await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(cancellationToken);
            var hmacHashAlgorithm = _settingsProvider.GetSetting(hmacHashAlgorithmSetting);
            var contentHashAlgorithm = _settingsProvider.GetSetting(contentHashAlgorithmSetting);
            var result = HmacHelper.Generate(_httpMethodsDropdown.SelectedItem!.Text!, _requestUrl.Text, _secret.Text, _requestContent.Text,
                contentHashAlgorithm, hmacHashAlgorithm, _logger);

            if (result.HasSucceeded)
            {
                var gridRows = new List<IUIDataGridRow>
                {
                    GUI.Row(result.Data, GUI.Cell("x-ms-date"), GUI.Cell(GUI.Label().WrapIfNeeded().Text(result.Data.Date))),
                    GUI.Row(result.Data, GUI.Cell($"x-ms-content-{HmacHelper.GetContentHashAlgorithm(contentHashAlgorithm)}"), GUI.Cell(GUI.Label().WrapIfNeeded().Text(result.Data.ContentHash))),
                    GUI.Row(result.Data, GUI.Cell("Authorization"), GUI.Cell(GUI.Label().WrapIfNeeded().Text(result.Data.AuthorizationHeader)))
                };
                _outputGrid.WithRows([.. gridRows]);
            }
            else
            {
                _infoBar.Title(result.ErrorMessage).Open().Show();
            }
        }
    }

    public void OnDataReceived(string dataTypeName, object? parsedData)
    {
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _semaphore.Dispose();
    }

    private void OnHmacHashAlgorithmChanged(HashAlgorithmEnum algorithm)
    {
        _settingsProvider.SetSetting(hmacHashAlgorithmSetting, algorithm);
    }

    private void OnContentHashAlgorithmChanged(HashAlgorithmEnum algorithm)
    {
        _settingsProvider.SetSetting(contentHashAlgorithmSetting, algorithm);
    }

    private static void InitializeHttpMethodsDropdown(IUISelectDropDownList selectDropDownList)
    {
        selectDropDownList
            .WithItems(
                GUI.Item("GET", HttpMethodType.GET),
                GUI.Item("POST", HttpMethodType.POST),
                GUI.Item("PUT", HttpMethodType.PUT),
                GUI.Item("PATCH", HttpMethodType.PATCH),
                GUI.Item("DELETE", HttpMethodType.DELETE)
            )
            .Select(0);
    }

    #region :: URL Stack ::

    private IUIStack UrlStack()
    {
        return
            GUI.Stack()
            .Vertical()
            .WithChildren(
                GUI.Grid("hmac-url-section")
                .RowSmallSpacing()
                .ColumnSmallSpacing()
                .Rows(
                    (UrlGridRow.Content, GUI.Auto)
                )
                .Columns(
                    (UrlGridColumn.Method, new UIGridLength(1, UIGridUnitType.Fraction)),
                    (UrlGridColumn.Url, new UIGridLength(6, UIGridUnitType.Fraction))
                )
                .Cells(
                    GUI.Cell(
                        UrlGridRow.Content,
                        UrlGridColumn.Method,
                        _httpMethodsDropdown
                            .Title("Http Method")
                    ),
                    GUI.Cell(
                        UrlGridRow.Content,
                        UrlGridColumn.Url,
                        _requestUrl
                            .Title("URL")
                            .HideCommandBar()
                            .CanCopyWhenEditable()
                    )
                )
            );
    }

    #endregion
}

internal enum MainGridColumn
{
    Content,
}

internal enum MainGridRow
{
    Banner,
    Options,
    Secret,
    Url,
    Content,
    Output
}

internal enum UrlGridRow
{
    Content,
}

internal enum UrlGridColumn
{
    Method,
    Url,
}

internal enum HttpMethodType
{
    GET,
    POST,
    PUT,
    PATCH,
    DELETE,
    OPTIONS
}