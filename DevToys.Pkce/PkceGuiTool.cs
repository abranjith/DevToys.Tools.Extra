using DevToys.Api;
using System.ComponentModel.Composition;
using Microsoft.Extensions.Logging;
using DevToys.Pkce.Helpers;

namespace DevToys.Pkce;

[Export(typeof(IGuiTool))]
[Name("Pkce")]
[ToolDisplayInformation(
    IconFontName = "FluentSystemIcons",
    IconGlyph = '\uE8C9',
    GroupName = "Generators",
    ResourceManagerAssemblyIdentifier = nameof(DevToysToolsExtraPkceResourceManagerAssemblyIdentifier),
    ResourceManagerBaseName = "DevToys.Pkce.Pkce",
    ShortDisplayTitleResourceName = nameof(Pkce.ShortDisplayTitle),
    LongDisplayTitleResourceName = nameof(Pkce.LongDisplayTitle),
    DescriptionResourceName = nameof(Pkce.Description),
    SearchKeywordsResourceName = nameof(Pkce.SearchKeywords),
    AccessibleNameResourceName = nameof(Pkce.AccessibleName))]
internal sealed partial class PkceGuiTool : IGuiTool, IDisposable
{
    private readonly DisposableSemaphore _semaphore = new();
    private readonly ILogger _logger;
    private readonly ISettingsProvider _settingsProvider;
    private CancellationTokenSource? _cancellationTokenSource;
    internal Task? WorkTask { get; private set; }

    [ImportingConstructor]
    public PkceGuiTool(ISettingsProvider settingsProvider)
    {
        _logger = this.Log();
        _settingsProvider = settingsProvider;

        _verifierTextArea.Text(string.Empty);
        _challengeTextArea.Text(string.Empty);
        _infoBar.Close().Hide();
    }

    #region :: Settings ::

    private static readonly SettingDefinition<int> verifierLength
        = new(
            name: $"{nameof(PkceGuiTool)}.{nameof(verifierLength)}",
            defaultValue: 128
            );

    #endregion

    #region :: UI Components ::

    //input
    private readonly IUINumberInput _pkceVerifierLength = GUI.NumberInput("pkce-verifier-length-area");

    //output
    private readonly IUIButton _generatePairButton = GUI.Button("pkce-generate-pair-btn");
    private readonly IUIMultiLineTextInput _verifierTextArea = GUI.MultiLineTextInput("pkce-verifier-text-area");
    private readonly IUIMultiLineTextInput _challengeTextArea = GUI.MultiLineTextInput("pkce-challenge-text-area");
    private readonly IUIInfoBar _infoBar = GUI.InfoBar("pkce-error-banner");

    #endregion

    #region :: View ::

    public UIToolView View
    => new(
        isScrollable: true,
        GUI.Grid("pkce-main-grid")
            .RowLargeSpacing()
            .Rows(
                (MainGridRow.Banner, GUI.Auto),
                (MainGridRow.Setting, new UIGridLength(1, UIGridUnitType.Fraction)),
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
                MainGridRow.Setting,
                MainGridColumn.Content,
                GUI.Setting()
                    .Title("Length")
                    .Description("Code Verifier Length")
                    .InteractiveElement(
                        _pkceVerifierLength
                            .HideCommandBar()
                            .Minimum(43)
                            .Maximum(128)
                            .OnValueChanged(OnLengthChanged)
                            .Value(_settingsProvider.GetSetting(verifierLength))
                    )
            ),
            GUI.Cell(
                MainGridRow.Output,
                MainGridColumn.Content,
                GUI.SplitGrid()
                    .Vertical()
                    .WithLeftPaneChild(
                        _verifierTextArea
                        .Title("Code Verifier")
                        .CanCopyWhenEditable()
                        .AlwaysWrap()
                        .OnTextChanged(OnVerifierTextChanged)
                        .CommandBarExtraContent(
                            _generatePairButton
                                .AccentAppearance()
                                .Text("Generate New")
                                .OnClick(OnGeneratePair)
                        )
                    )
                    .WithRightPaneChild(
                        _challengeTextArea
                        .Title("Code Challenge")
                        .AlwaysWrap()
                        .ReadOnly()
                    )
                )
            )
    );

    #endregion

    private void OnLengthChanged(double value)
    {
        _settingsProvider.SetSetting(verifierLength, (int)value);
        OnGenerateRequest();
    }

    private void OnVerifierTextChanged(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
        {
            _challengeTextArea.Text(string.Empty);
        }
        else
        {
            OnGenerateRequest(_verifierTextArea.Text);
        }
    }

    private void OnGeneratePair()
        => OnGenerateRequest();

    private void OnGenerateRequest(string? verifier = null)
    {
        _infoBar.Close().Hide();

        int length = _settingsProvider.GetSetting(verifierLength);
        var result = PkceHelper.Generate(length, verifier, _logger);

        if(result.HasSucceeded)
        {
            if(string.IsNullOrWhiteSpace(verifier))
            {
                _verifierTextArea.Text(result.Data.CodeVerifier ?? string.Empty);
            }
            _challengeTextArea.Text(result.Data.CodeChallenge ?? string.Empty);
        }
        else
        {
            _infoBar.Title(result.ErrorMessage).Open().Show();
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
}


#region :: Grid ::

internal enum MainGridColumn
{
    Content,
}

internal enum MainGridRow
{
    Banner,
    Setting,
    Output,
}

#endregion