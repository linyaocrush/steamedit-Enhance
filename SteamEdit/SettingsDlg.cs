using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Common;
using Microsoft.Win32;
using SteamEdit.Properties;

namespace SteamEdit;

internal class SettingsDlg : Form
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SteamEdit";

    private IContainer components;

    private CheckBox checkEnableCompleted;

    private ToolTip toolTip;

    private CheckBox checkEnabledHidden;

    private TextBox textPassThroughArgs;

    private TextBox textAppInfoPath;

    private CheckBox checkReplaceLocalizedNames;

    private ComboBox comboLanguage;

    private Label lblLanguage;

    private Label label1;

    private Label label2;

    private Label label4;

    private Label label5;

    private Label label6;

    private Label lblAutoStart;

    private CheckBox checkAutoStart;

    private CheckBox checkAutoStartAutoFix;

    private CheckBox checkAutoStartForceStart;

    private FlowLayoutPanel panelAutoStartSub;

    private Label lblAiApiUrl;

    private TextBox textAiApiUrl;

    private Label lblAiApiKey;

    private TextBox textAiApiKey;

    private Label lblAiTargetLanguage;

    private ComboBox comboAiTargetLanguage;

    private Label lblAiModel;

    private ComboBox comboAiModel;

    private Button btnFetchModels;

    private Label lblSearchEngine;

    private ComboBox comboSearchEngine;

    private Label lblCustomSystemPrompt;
    private TextBox textCustomSystemPrompt;
    private Button btnResetPrompt;
    private Label lblCustomUserMessage;
    private TextBox textCustomUserMessage;
    private Button btnResetUserMessage;
    private Label lblTranslationRules;
    private TextBox textTranslationRules;
    private Button btnAnalyzeRules;
    private Button btnClearRules;

    private bool _loading;
    private bool _snapshotAutoStart;
    private bool _snapshotAutoFix;
    private bool _snapshotForceStart;

    public SettingsDlg()
    {
        InitializeComponent();
    }

    private void Settings_Load(object sender, EventArgs e)
    {
        _loading = true;
        checkEnableCompleted.Checked = Settings.Default.EnableCompleted;
        checkEnabledHidden.Checked = Settings.Default.EnableHidden;
        checkReplaceLocalizedNames.Checked = Settings.Default.ReplaceLocalizedNames;
        textPassThroughArgs.Text = Settings.Default.PassThroughArguments;
        textAppInfoPath.Text = Settings.Default.AppInfoPath;
        checkAutoStart.Checked = Settings.Default.AutoStart;
        checkAutoStartAutoFix.Checked = Settings.Default.AutoStartAutoFix;
        checkAutoStartForceStart.Checked = Settings.Default.AutoStartForceStart;
        _snapshotAutoStart = Settings.Default.AutoStart;
        _snapshotAutoFix = Settings.Default.AutoStartAutoFix;
        _snapshotForceStart = Settings.Default.AutoStartForceStart;
        textAiApiUrl.Text = Settings.Default.AiApiUrl;
        textAiApiKey.Text = Settings.Default.AiApiKey;
        textCustomSystemPrompt.Text = string.IsNullOrWhiteSpace(Settings.Default.AiCustomSystemPrompt)
            ? AiTranslator.DefaultSystemPrompt : Settings.Default.AiCustomSystemPrompt;
        textCustomUserMessage.Text = string.IsNullOrWhiteSpace(Settings.Default.AiCustomUserMessage)
            ? AiTranslator.DefaultUserMessage : Settings.Default.AiCustomUserMessage;
        textTranslationRules.Text = Settings.Default.AiTranslationRules;
        _loading = false;
        UpdateAutoStartSubEnabled();

        string savedLang = Settings.Default.Language;
        if (string.IsNullOrEmpty(savedLang))
        {
            savedLang = System.Globalization.CultureInfo.CurrentUICulture.Name;
        }
        if (savedLang == "zh-TW" || savedLang.StartsWith("zh-TW"))
        {
            comboLanguage.SelectedIndex = 2;
        }
        else if (savedLang.StartsWith("zh"))
        {
            comboLanguage.SelectedIndex = 1;
        }
        else
        {
            comboLanguage.SelectedIndex = 0;
        }

        string aiLang = Settings.Default.AiTargetLanguage;
        comboAiTargetLanguage.SelectedIndex = aiLang == "en" ? 1 : 0;

        string aiModel = Settings.Default.AiModel;
        comboAiModel.Items.Add(aiModel);
        comboAiModel.SelectedIndex = 0;

        string searchEngine = Settings.Default.SearchEngine;
        int seIdx = comboSearchEngine.Items.IndexOf(searchEngine);
        comboSearchEngine.SelectedIndex = seIdx >= 0 ? seIdx : 0;

        ApplyLanguage();
    }

    private void SettingsDlg_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (base.DialogResult == DialogResult.OK)
        {
            Settings.Default.EnableCompleted = checkEnableCompleted.Checked;
            Settings.Default.EnableHidden = checkEnabledHidden.Checked;
            Settings.Default.ReplaceLocalizedNames = checkReplaceLocalizedNames.Checked;
            Settings.Default.PassThroughArguments = textPassThroughArgs.Text;
            Settings.Default.AppInfoPath = textAppInfoPath.Text;
            Settings.Default.AutoStart = checkAutoStart.Checked;
            Settings.Default.AutoStartAutoFix = checkAutoStartAutoFix.Checked;
            Settings.Default.AutoStartForceStart = checkAutoStartForceStart.Checked;
            Settings.Default.AiApiUrl = textAiApiUrl.Text;
            Settings.Default.AiApiKey = textAiApiKey.Text;
            Settings.Default.AiTargetLanguage = comboAiTargetLanguage.SelectedIndex == 1 ? "en" : I18n.CurrentCulture.Name;
            Settings.Default.AiModel = comboAiModel.Text;
            Settings.Default.AiCustomSystemPrompt = textCustomSystemPrompt.Text == AiTranslator.DefaultSystemPrompt ? "" : textCustomSystemPrompt.Text;
            Settings.Default.AiCustomUserMessage = textCustomUserMessage.Text == AiTranslator.DefaultUserMessage ? "" : textCustomUserMessage.Text;
            Settings.Default.AiTranslationRules = textTranslationRules.Text;
            Settings.Default.SearchEngine = comboSearchEngine.Text;
            ApplyAutoStartRegistry(checkAutoStart.Checked, checkAutoStartAutoFix.Checked, checkAutoStartForceStart.Checked);
            Settings.Default.Save();
        }
        else
        {
            ApplyAutoStartRegistry(_snapshotAutoStart, _snapshotAutoFix, _snapshotForceStart);
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        base.DialogResult = DialogResult.OK;
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        base.DialogResult = DialogResult.Cancel;
    }

    private void btnSelectAppInfo_Click(object sender, EventArgs e)
    {
        using OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.CheckFileExists = true;
        openFileDialog.Filter = "appinfo.vdf|appinfo.vdf";
        openFileDialog.Multiselect = false;
        openFileDialog.InitialDirectory = Path.GetDirectoryName(textAppInfoPath.Text);
        openFileDialog.FileName = Path.GetFileName(textAppInfoPath.Text);
        if (openFileDialog.ShowDialog(this) == DialogResult.OK)
        {
            textAppInfoPath.Text = openFileDialog.FileName;
        }
    }

    private async void btnFetchModels_Click(object sender, EventArgs e)
    {
        btnFetchModels.Enabled = false;
        btnFetchModels.Text = "...";
        try
        {
            var models = await AiTranslator.FetchModelsAsync(textAiApiUrl.Text, textAiApiKey.Text);
            string current = comboAiModel.Text;
            comboAiModel.Items.Clear();
            foreach (var m in models) comboAiModel.Items.Add(m);
            if (comboAiModel.Items.Contains(current))
                comboAiModel.SelectedItem = current;
            else if (comboAiModel.Items.Count > 0)
                comboAiModel.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnFetchModels.Text = Strings.Settings_AiFetchModels;
            btnFetchModels.Enabled = true;
        }
    }

    private void btnResetPrompt_Click(object sender, EventArgs e)
    {
        textCustomSystemPrompt.Text = AiTranslator.DefaultSystemPrompt;
    }

    private void btnResetUserMessage_Click(object sender, EventArgs e)
    {
        textCustomUserMessage.Text = AiTranslator.DefaultUserMessage;
    }

    private void btnClearRules_Click(object sender, EventArgs e)
    {
        textTranslationRules.Text = "";
    }

    private async void btnAnalyzeRules_Click(object sender, EventArgs e)
    {
        string glossaryPath = Path.Combine(AppHelper.GetAppDirectory(), "glossary.json");
        if (!File.Exists(glossaryPath))
        {
            MessageBox.Show(this, Strings.Msg_NoGlossaryData, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string glossaryContent;
        try
        {
            glossaryContent = File.ReadAllText(glossaryPath, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(glossaryContent) || glossaryContent.Length < 20)
        {
            MessageBox.Show(this, Strings.Msg_NoGlossaryData, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnAnalyzeRules.Enabled = false;
        btnAnalyzeRules.Text = Strings.Msg_AnalyzingRules;
        try
        {
            string currentSystemPrompt = textCustomSystemPrompt.Text;
            string rules = await AiTranslator.AnalyzeTranslationRulesAsync(glossaryContent, currentSystemPrompt);
            if (!string.IsNullOrWhiteSpace(rules))
            {
                textTranslationRules.Text = rules;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnAnalyzeRules.Text = Strings.Settings_AnalyzeRules;
            btnAnalyzeRules.Enabled = true;
        }
    }

    private void comboLanguage_SelectedIndexChanged(object sender, EventArgs e)
    {
        string cultureCode = comboLanguage.SelectedIndex switch
        {
            1 => "zh-CN",
            2 => "zh-TW",
            _ => "en"
        };
        I18n.SetLanguage(cultureCode);
    }

    private void checkAutoStart_CheckedChanged(object sender, EventArgs e)
    {
        if (_loading) return;
        UpdateAutoStartSubEnabled();
        ApplyAutoStartRegistry(checkAutoStart.Checked, checkAutoStartAutoFix.Checked, checkAutoStartForceStart.Checked);
    }

    private void checkAutoStartAutoFix_CheckedChanged(object sender, EventArgs e)
    {
        if (_loading) return;
        if (!checkAutoStartAutoFix.Checked)
        {
            checkAutoStartForceStart.Checked = false;
        }
        ApplyAutoStartRegistry(checkAutoStart.Checked, checkAutoStartAutoFix.Checked, checkAutoStartForceStart.Checked);
    }

    private void checkAutoStartForceStart_CheckedChanged(object sender, EventArgs e)
    {
        if (_loading) return;
        if (checkAutoStartForceStart.Checked)
        {
            checkAutoStartAutoFix.Checked = true;
        }
        ApplyAutoStartRegistry(checkAutoStart.Checked, checkAutoStartAutoFix.Checked, checkAutoStartForceStart.Checked);
    }

    private void UpdateAutoStartSubEnabled()
    {
        bool enabled = checkAutoStart.Checked;
        checkAutoStartAutoFix.Enabled = enabled;
        checkAutoStartForceStart.Enabled = enabled;
        if (!enabled)
        {
            checkAutoStartAutoFix.Checked = false;
            checkAutoStartForceStart.Checked = false;
        }
    }

    private static void ApplyAutoStartRegistry(bool autoStart, bool autoFix, bool forceStart)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (autoStart)
            {
                string exePath = Application.ExecutablePath;
                string args = "";
                if (autoFix) args += " -autofix";
                if (forceStart) args += " -forcestart";
                key.SetValue(RunValueName, $"\"{exePath}\"{args}");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.WriteLine("Failed to update auto-start registry: {0}", ex.Message);
        }
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ApplyLanguage));
            return;
        }
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = Strings.Settings_Title;
        label1.Text = Strings.Settings_EnableCompleted;
        label2.Text = Strings.Settings_EnableHidden;
        lblLanguage.Text = Strings.Settings_Language;
        label4.Text = Strings.Settings_PassThroughArgs;
        label5.Text = Strings.Settings_ReplaceLocalizedNames;
        label6.Text = Strings.Settings_AppInfoPath;
        lblAutoStart.Text = Strings.Settings_AutoStart;
        checkAutoStart.Text = "";
        checkAutoStartAutoFix.Text = Strings.Settings_AutoStartAutoFix;
        checkAutoStartForceStart.Text = Strings.Settings_AutoStartForceStart;
        checkReplaceLocalizedNames.Text = "";
        lblAiApiUrl.Text = Strings.Settings_AiApiUrl;
        lblAiApiKey.Text = Strings.Settings_AiApiKey;
        lblAiTargetLanguage.Text = Strings.Settings_AiTargetLanguage;
        comboAiTargetLanguage.Items[0] = Strings.AiLang_zhCN;
        comboAiTargetLanguage.Items[1] = Strings.AiLang_en;
        lblAiModel.Text = Strings.Settings_AiModel;
        btnFetchModels.Text = Strings.Settings_AiFetchModels;
        lblSearchEngine.Text = Strings.Settings_SearchEngine;
        lblCustomSystemPrompt.Text = Strings.Settings_CustomSystemPrompt;
        lblCustomUserMessage.Text = Strings.Settings_CustomUserMessage;
        lblTranslationRules.Text = Strings.Settings_TranslationRules;
        btnResetPrompt.Text = Strings.Settings_ResetPrompt;
        btnResetUserMessage.Text = Strings.Settings_ResetPrompt;
        btnAnalyzeRules.Text = Strings.Settings_AnalyzeRules;
        btnClearRules.Text = Strings.Settings_ClearRules;
        toolTip.SetToolTip(textCustomSystemPrompt, Strings.Tooltip_CustomSystemPrompt);
        toolTip.SetToolTip(textCustomUserMessage, Strings.Tooltip_CustomUserMessage);
        toolTip.SetToolTip(textTranslationRules, Strings.Tooltip_TranslationRules);
        toolTip.SetToolTip(checkReplaceLocalizedNames, Strings.Tooltip_ReplaceLocalizedNames);
        toolTip.SetToolTip(checkEnabledHidden, Strings.Tooltip_EnableHidden);
        toolTip.SetToolTip(checkEnableCompleted, Strings.Tooltip_EnableCompleted);
        toolTip.SetToolTip(textAppInfoPath, Strings.Tooltip_AppInfoPath);
        toolTip.SetToolTip(textPassThroughArgs, Strings.Tooltip_PassThroughArgs);
        toolTip.SetToolTip(checkAutoStart, Strings.Tooltip_AutoStart);
        toolTip.SetToolTip(checkAutoStartAutoFix, Strings.Tooltip_AutoStartAutoFix);
        toolTip.SetToolTip(checkAutoStartForceStart, Strings.Tooltip_AutoStartForceStart);
        toolTip.SetToolTip(textAiApiUrl, Strings.Tooltip_AiApiUrl);
        toolTip.SetToolTip(textAiApiKey, Strings.Tooltip_AiApiKey);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        I18n.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        I18n.LanguageChanged -= OnLanguageChanged;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.checkReplaceLocalizedNames = new System.Windows.Forms.CheckBox();
        this.checkEnabledHidden = new System.Windows.Forms.CheckBox();
        this.checkEnableCompleted = new System.Windows.Forms.CheckBox();
        this.textAppInfoPath = new System.Windows.Forms.TextBox();
        this.textPassThroughArgs = new System.Windows.Forms.TextBox();
        this.comboLanguage = new System.Windows.Forms.ComboBox();
        this.lblLanguage = new System.Windows.Forms.Label();
        this.toolTip = new System.Windows.Forms.ToolTip(this.components);
        System.Windows.Forms.Button button = new System.Windows.Forms.Button();
        this.label1 = new System.Windows.Forms.Label();
        this.label2 = new System.Windows.Forms.Label();
        this.label4 = new System.Windows.Forms.Label();
        System.Windows.Forms.TableLayoutPanel tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
        this.label5 = new System.Windows.Forms.Label();
        System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
        System.Windows.Forms.Button button2 = new System.Windows.Forms.Button();
        this.label6 = new System.Windows.Forms.Label();
        this.lblAutoStart = new System.Windows.Forms.Label();
        this.checkAutoStart = new System.Windows.Forms.CheckBox();
        this.checkAutoStartAutoFix = new System.Windows.Forms.CheckBox();
        this.checkAutoStartForceStart = new System.Windows.Forms.CheckBox();
        this.panelAutoStartSub = new System.Windows.Forms.FlowLayoutPanel();
        this.lblAiApiUrl = new System.Windows.Forms.Label();
        this.textAiApiUrl = new System.Windows.Forms.TextBox();
        this.lblAiApiKey = new System.Windows.Forms.Label();
        this.textAiApiKey = new System.Windows.Forms.TextBox();
        this.lblAiTargetLanguage = new System.Windows.Forms.Label();
        this.comboAiTargetLanguage = new System.Windows.Forms.ComboBox();
        this.lblAiModel = new System.Windows.Forms.Label();
        this.comboAiModel = new System.Windows.Forms.ComboBox();
        this.btnFetchModels = new System.Windows.Forms.Button();
        this.lblSearchEngine = new System.Windows.Forms.Label();
        this.comboSearchEngine = new System.Windows.Forms.ComboBox();
        this.lblCustomSystemPrompt = new System.Windows.Forms.Label();
        this.textCustomSystemPrompt = new System.Windows.Forms.TextBox();
        this.btnResetPrompt = new System.Windows.Forms.Button();
        this.lblCustomUserMessage = new System.Windows.Forms.Label();
        this.textCustomUserMessage = new System.Windows.Forms.TextBox();
        this.btnResetUserMessage = new System.Windows.Forms.Button();
        this.lblTranslationRules = new System.Windows.Forms.Label();
        this.textTranslationRules = new System.Windows.Forms.TextBox();
        this.btnAnalyzeRules = new System.Windows.Forms.Button();
        this.btnClearRules = new System.Windows.Forms.Button();
        System.Windows.Forms.Panel panelPrompt = new System.Windows.Forms.Panel();
        System.Windows.Forms.Panel panelUserMsg = new System.Windows.Forms.Panel();
        System.Windows.Forms.Panel panelRules = new System.Windows.Forms.Panel();
        System.Windows.Forms.Panel panelModel = new System.Windows.Forms.Panel();
        System.Windows.Forms.Button button3 = new System.Windows.Forms.Button();
        tableLayoutPanel.SuspendLayout();
        panel.SuspendLayout();
        this.panelAutoStartSub.SuspendLayout();
        panelModel.SuspendLayout();
        panelPrompt.SuspendLayout();
        panelUserMsg.SuspendLayout();
        panelRules.SuspendLayout();
        base.SuspendLayout();
        // btnOK
        button.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        button.DialogResult = System.Windows.Forms.DialogResult.OK;
        button.Location = new System.Drawing.Point(12, 735);
        button.Name = "btnOK";
        button.Size = new System.Drawing.Size(88, 30);
        button.TabIndex = 3;
        button.Text = "OK";
        button.UseVisualStyleBackColor = true;
        button.Click += new System.EventHandler(btnOK_Click);
        // label1 (Completed)
        this.label1.AutoSize = true;
        this.label1.Margin = new System.Windows.Forms.Padding(6);
        this.label1.Name = "label1";
        this.label1.TabIndex = 0;
        this.label1.Text = "Enable \"Completed\"";
        // label2 (Hidden)
        this.label2.AutoSize = true;
        this.label2.Margin = new System.Windows.Forms.Padding(6);
        this.label2.Name = "label2";
        this.label2.TabIndex = 1;
        this.label2.Text = "Enable \"Hidden\"";
        // lblLanguage
        this.lblLanguage.AutoSize = true;
        this.lblLanguage.Margin = new System.Windows.Forms.Padding(6);
        this.lblLanguage.Name = "lblLanguage";
        this.lblLanguage.TabIndex = 10;
        this.lblLanguage.Text = "Language:";
        // comboLanguage
        this.comboLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.comboLanguage.FormattingEnabled = true;
        this.comboLanguage.Items.AddRange(new object[] { "English", "简体中文", "繁體中文" });
        this.comboLanguage.Name = "comboLanguage";
        this.comboLanguage.Size = new System.Drawing.Size(121, 21);
        this.comboLanguage.TabIndex = 11;
        this.comboLanguage.SelectedIndexChanged += new System.EventHandler(comboLanguage_SelectedIndexChanged);
        // label5 (ReplaceLocalizedNames)
        this.label5.AutoSize = true;
        this.label5.Margin = new System.Windows.Forms.Padding(6);
        this.label5.Name = "label5";
        this.label5.TabIndex = 9;
        this.label5.Text = "Replace All Localized Names";
        // checkReplaceLocalizedNames
        this.checkReplaceLocalizedNames.AutoSize = true;
        this.checkReplaceLocalizedNames.Margin = new System.Windows.Forms.Padding(6);
        this.checkReplaceLocalizedNames.Name = "checkReplaceLocalizedNames";
        this.checkReplaceLocalizedNames.TabIndex = 8;
        this.toolTip.SetToolTip(this.checkReplaceLocalizedNames, "Replace all localized game names with the original name.");
        this.checkReplaceLocalizedNames.UseVisualStyleBackColor = true;
        // checkEnabledHidden
        this.checkEnabledHidden.AutoSize = true;
        this.checkEnabledHidden.Margin = new System.Windows.Forms.Padding(6);
        this.checkEnabledHidden.Name = "checkEnabledHidden";
        this.checkEnabledHidden.TabIndex = 1;
        this.toolTip.SetToolTip(this.checkEnabledHidden, "Hide games in Steam's list.");
        this.checkEnabledHidden.UseVisualStyleBackColor = true;
        // checkEnableCompleted
        this.checkEnableCompleted.AutoSize = true;
        this.checkEnableCompleted.Margin = new System.Windows.Forms.Padding(6);
        this.checkEnableCompleted.Name = "checkEnableCompleted";
        this.checkEnableCompleted.TabIndex = 0;
        this.toolTip.SetToolTip(this.checkEnableCompleted, "Append symbol on completed games");
        this.checkEnableCompleted.UseVisualStyleBackColor = true;
        // label4 (PassThrough)
        this.label4.AutoSize = true;
        this.label4.Margin = new System.Windows.Forms.Padding(6);
        this.label4.Name = "label4";
        this.label4.TabIndex = 4;
        this.label4.Text = "Pass-Through Arguments";
        // textPassThroughArgs
        this.textPassThroughArgs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textPassThroughArgs.Name = "textPassThroughArgs";
        this.textPassThroughArgs.Size = new System.Drawing.Size(224, 20);
        this.textPassThroughArgs.TabIndex = 5;
        this.toolTip.SetToolTip(this.textPassThroughArgs, "Arguments to pass along when launching Steam...");
        // panel (AppInfo Path)
        panel.Controls.Add(button2);
        panel.Controls.Add(this.textAppInfoPath);
        panel.Dock = System.Windows.Forms.DockStyle.Fill;
        panel.Margin = new System.Windows.Forms.Padding(0);
        panel.Name = "panel1";
        panel.TabIndex = 7;
        // btnSelectAppInfo
        button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        button2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
        button2.Image = SteamEdit.Properties.Resources.openfolderHS;
        button2.Margin = new System.Windows.Forms.Padding(0);
        button2.Name = "btnSelectAppInfo";
        button2.Size = new System.Drawing.Size(26, 25);
        button2.TabIndex = 12;
        button2.UseVisualStyleBackColor = true;
        button2.Click += new System.EventHandler(btnSelectAppInfo_Click);
        // textAppInfoPath
        this.textAppInfoPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textAppInfoPath.Name = "textAppInfoPath";
        this.textAppInfoPath.Size = new System.Drawing.Size(195, 20);
        this.textAppInfoPath.TabIndex = 7;
        this.toolTip.SetToolTip(this.textAppInfoPath, "Path to Steam's appinfo.vdf file");
        // label6 (AppInfo Path)
        this.label6.AutoSize = true;
        this.label6.Margin = new System.Windows.Forms.Padding(6);
        this.label6.Name = "label6";
        this.label6.TabIndex = 6;
        this.label6.Text = "AppInfo Path (requires restart)";
        // lblAutoStart
        this.lblAutoStart.AutoSize = true;
        this.lblAutoStart.Margin = new System.Windows.Forms.Padding(6);
        this.lblAutoStart.Name = "lblAutoStart";
        this.lblAutoStart.TabIndex = 13;
        this.lblAutoStart.Text = "Start on Boot";
        // checkAutoStart
        this.checkAutoStart.AutoSize = true;
        this.checkAutoStart.Margin = new System.Windows.Forms.Padding(3, 5, 8, 3);
        this.checkAutoStart.Name = "checkAutoStart";
        this.checkAutoStart.TabIndex = 14;
        this.toolTip.SetToolTip(this.checkAutoStart, "Add SteamEdit to Windows startup registry");
        this.checkAutoStart.UseVisualStyleBackColor = true;
        this.checkAutoStart.CheckedChanged += new System.EventHandler(checkAutoStart_CheckedChanged);
        // checkAutoStartAutoFix
        this.checkAutoStartAutoFix.AutoSize = true;
        this.checkAutoStartAutoFix.Enabled = false;
        this.checkAutoStartAutoFix.Margin = new System.Windows.Forms.Padding(3, 5, 8, 3);
        this.checkAutoStartAutoFix.Name = "checkAutoStartAutoFix";
        this.checkAutoStartAutoFix.TabIndex = 15;
        this.checkAutoStartAutoFix.Text = "autofix";
        this.toolTip.SetToolTip(this.checkAutoStartAutoFix, "Auto-apply pending changes on startup (autofix)");
        this.checkAutoStartAutoFix.UseVisualStyleBackColor = true;
        this.checkAutoStartAutoFix.CheckedChanged += new System.EventHandler(checkAutoStartAutoFix_CheckedChanged);
        // checkAutoStartForceStart
        this.checkAutoStartForceStart.AutoSize = true;
        this.checkAutoStartForceStart.Enabled = false;
        this.checkAutoStartForceStart.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
        this.checkAutoStartForceStart.Name = "checkAutoStartForceStart";
        this.checkAutoStartForceStart.TabIndex = 16;
        this.checkAutoStartForceStart.Text = "forcestart";
        this.toolTip.SetToolTip(this.checkAutoStartForceStart, "Also restart Steam after applying changes (forcestart). Requires autofix.");
        this.checkAutoStartForceStart.UseVisualStyleBackColor = true;
        this.checkAutoStartForceStart.CheckedChanged += new System.EventHandler(checkAutoStartForceStart_CheckedChanged);
        // panelAutoStartSub
        this.panelAutoStartSub.Controls.Add(this.checkAutoStart);
        this.panelAutoStartSub.Controls.Add(this.checkAutoStartAutoFix);
        this.panelAutoStartSub.Controls.Add(this.checkAutoStartForceStart);
        this.panelAutoStartSub.Dock = System.Windows.Forms.DockStyle.Fill;
        this.panelAutoStartSub.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        this.panelAutoStartSub.AutoSize = true;
        this.panelAutoStartSub.WrapContents = false;
        this.panelAutoStartSub.Margin = new System.Windows.Forms.Padding(0);
        this.panelAutoStartSub.Name = "panelAutoStartSub";
        this.panelAutoStartSub.TabIndex = 17;
        // lblAiApiUrl
        this.lblAiApiUrl.AutoSize = true;
        this.lblAiApiUrl.Margin = new System.Windows.Forms.Padding(6);
        this.lblAiApiUrl.Name = "lblAiApiUrl";
        this.lblAiApiUrl.TabIndex = 18;
        this.lblAiApiUrl.Text = "AI API URL";
        // textAiApiUrl
        this.textAiApiUrl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textAiApiUrl.Name = "textAiApiUrl";
        this.textAiApiUrl.Size = new System.Drawing.Size(224, 20);
        this.textAiApiUrl.TabIndex = 19;
        this.toolTip.SetToolTip(this.textAiApiUrl, "OpenAI-compatible API endpoint");
        // lblAiApiKey
        this.lblAiApiKey.AutoSize = true;
        this.lblAiApiKey.Margin = new System.Windows.Forms.Padding(6);
        this.lblAiApiKey.Name = "lblAiApiKey";
        this.lblAiApiKey.TabIndex = 20;
        this.lblAiApiKey.Text = "AI API Key";
        // textAiApiKey
        this.textAiApiKey.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textAiApiKey.Name = "textAiApiKey";
        this.textAiApiKey.Size = new System.Drawing.Size(224, 20);
        this.textAiApiKey.TabIndex = 21;
        this.textAiApiKey.UseSystemPasswordChar = true;
        this.toolTip.SetToolTip(this.textAiApiKey, "API key for AI translation service");
        // lblAiTargetLanguage
        this.lblAiTargetLanguage.AutoSize = true;
        this.lblAiTargetLanguage.Margin = new System.Windows.Forms.Padding(6);
        this.lblAiTargetLanguage.Name = "lblAiTargetLanguage";
        this.lblAiTargetLanguage.TabIndex = 22;
        this.lblAiTargetLanguage.Text = "AI Target Language";
        // comboAiTargetLanguage
        this.comboAiTargetLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.comboAiTargetLanguage.FormattingEnabled = true;
        this.comboAiTargetLanguage.Items.AddRange(new object[] { "中文", "English" });
        this.comboAiTargetLanguage.Name = "comboAiTargetLanguage";
        this.comboAiTargetLanguage.Size = new System.Drawing.Size(121, 21);
        this.comboAiTargetLanguage.TabIndex = 23;
        // lblAiModel
        this.lblAiModel.AutoSize = true;
        this.lblAiModel.Margin = new System.Windows.Forms.Padding(6);
        this.lblAiModel.Name = "lblAiModel";
        this.lblAiModel.TabIndex = 24;
        this.lblAiModel.Text = "AI Model";
        // comboAiModel
        this.comboAiModel.FormattingEnabled = true;
        this.comboAiModel.Name = "comboAiModel";
        this.comboAiModel.Size = new System.Drawing.Size(160, 21);
        this.comboAiModel.TabIndex = 25;
        this.comboAiModel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        // btnFetchModels
        this.btnFetchModels.Name = "btnFetchModels";
        this.btnFetchModels.Size = new System.Drawing.Size(60, 23);
        this.btnFetchModels.TabIndex = 26;
        this.btnFetchModels.Text = "Fetch";
        this.btnFetchModels.UseVisualStyleBackColor = true;
        this.btnFetchModels.Click += new System.EventHandler(btnFetchModels_Click);
        this.comboAiModel.Location = new System.Drawing.Point(0, 3);
        this.comboAiModel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.btnFetchModels.Location = new System.Drawing.Point(163, 2);
        this.btnFetchModels.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        // panelModel (ComboBox + Button)
        panelModel.Controls.Add(this.comboAiModel);
        panelModel.Controls.Add(this.btnFetchModels);
        panelModel.Dock = System.Windows.Forms.DockStyle.Fill;
        panelModel.Margin = new System.Windows.Forms.Padding(0);
        panelModel.Name = "panelModel";
        panelModel.TabIndex = 27;
        // lblCustomSystemPrompt
        this.lblCustomSystemPrompt.AutoSize = true;
        this.lblCustomSystemPrompt.Margin = new System.Windows.Forms.Padding(6);
        this.lblCustomSystemPrompt.Name = "lblCustomSystemPrompt";
        this.lblCustomSystemPrompt.TabIndex = 30;
        this.lblCustomSystemPrompt.Text = "Custom System Prompt";
        // textCustomSystemPrompt
        this.textCustomSystemPrompt.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textCustomSystemPrompt.Multiline = true;
        this.textCustomSystemPrompt.Name = "textCustomSystemPrompt";
        this.textCustomSystemPrompt.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.textCustomSystemPrompt.Size = new System.Drawing.Size(224, 80);
        this.textCustomSystemPrompt.TabIndex = 31;
        // btnResetPrompt
        this.btnResetPrompt.Name = "btnResetPrompt";
        this.btnResetPrompt.Size = new System.Drawing.Size(80, 23);
        this.btnResetPrompt.TabIndex = 32;
        this.btnResetPrompt.Text = "Reset";
        this.btnResetPrompt.UseVisualStyleBackColor = true;
        this.btnResetPrompt.Click += new System.EventHandler(btnResetPrompt_Click);
        // panelPrompt
        panelPrompt.Controls.Add(this.textCustomSystemPrompt);
        panelPrompt.Controls.Add(this.btnResetPrompt);
        panelPrompt.Dock = System.Windows.Forms.DockStyle.Fill;
        panelPrompt.Margin = new System.Windows.Forms.Padding(0);
        panelPrompt.Name = "panelPrompt";
        panelPrompt.TabIndex = 33;
        this.textCustomSystemPrompt.Location = new System.Drawing.Point(0, 0);
        this.btnResetPrompt.Location = new System.Drawing.Point(0, 83);
        this.btnResetPrompt.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        // lblCustomUserMessage
        this.lblCustomUserMessage.AutoSize = true;
        this.lblCustomUserMessage.Margin = new System.Windows.Forms.Padding(6);
        this.lblCustomUserMessage.Name = "lblCustomUserMessage";
        this.lblCustomUserMessage.TabIndex = 34;
        this.lblCustomUserMessage.Text = "Custom User Message";
        // textCustomUserMessage
        this.textCustomUserMessage.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textCustomUserMessage.Multiline = true;
        this.textCustomUserMessage.Name = "textCustomUserMessage";
        this.textCustomUserMessage.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.textCustomUserMessage.Size = new System.Drawing.Size(224, 50);
        this.textCustomUserMessage.TabIndex = 35;
        // btnResetUserMessage
        this.btnResetUserMessage.Name = "btnResetUserMessage";
        this.btnResetUserMessage.Size = new System.Drawing.Size(80, 23);
        this.btnResetUserMessage.TabIndex = 36;
        this.btnResetUserMessage.Text = "Reset";
        this.btnResetUserMessage.UseVisualStyleBackColor = true;
        this.btnResetUserMessage.Click += new System.EventHandler(btnResetUserMessage_Click);
        // panelUserMsg
        panelUserMsg.Controls.Add(this.textCustomUserMessage);
        panelUserMsg.Controls.Add(this.btnResetUserMessage);
        panelUserMsg.Dock = System.Windows.Forms.DockStyle.Fill;
        panelUserMsg.Margin = new System.Windows.Forms.Padding(0);
        panelUserMsg.Name = "panelUserMsg";
        panelUserMsg.TabIndex = 37;
        this.textCustomUserMessage.Location = new System.Drawing.Point(0, 0);
        this.btnResetUserMessage.Location = new System.Drawing.Point(0, 53);
        this.btnResetUserMessage.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        // lblTranslationRules
        this.lblTranslationRules.AutoSize = true;
        this.lblTranslationRules.Margin = new System.Windows.Forms.Padding(6);
        this.lblTranslationRules.Name = "lblTranslationRules";
        this.lblTranslationRules.TabIndex = 38;
        this.lblTranslationRules.Text = "Translation Rules (AI Summary)";
        // textTranslationRules
        this.textTranslationRules.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textTranslationRules.Multiline = true;
        this.textTranslationRules.Name = "textTranslationRules";
        this.textTranslationRules.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.textTranslationRules.Size = new System.Drawing.Size(224, 60);
        this.textTranslationRules.TabIndex = 39;
        // btnAnalyzeRules
        this.btnAnalyzeRules.Name = "btnAnalyzeRules";
        this.btnAnalyzeRules.Size = new System.Drawing.Size(80, 23);
        this.btnAnalyzeRules.TabIndex = 40;
        this.btnAnalyzeRules.Text = "AI Analyze";
        this.btnAnalyzeRules.UseVisualStyleBackColor = true;
        this.btnAnalyzeRules.Click += new System.EventHandler(btnAnalyzeRules_Click);
        // btnClearRules
        this.btnClearRules.Name = "btnClearRules";
        this.btnClearRules.Size = new System.Drawing.Size(60, 23);
        this.btnClearRules.TabIndex = 41;
        this.btnClearRules.Text = "Clear";
        this.btnClearRules.UseVisualStyleBackColor = true;
        this.btnClearRules.Click += new System.EventHandler(btnClearRules_Click);
        // panelRules
        panelRules.Controls.Add(this.textTranslationRules);
        panelRules.Controls.Add(this.btnAnalyzeRules);
        panelRules.Controls.Add(this.btnClearRules);
        panelRules.Dock = System.Windows.Forms.DockStyle.Fill;
        panelRules.Margin = new System.Windows.Forms.Padding(0);
        panelRules.Name = "panelRules";
        panelRules.TabIndex = 42;
        this.textTranslationRules.Location = new System.Drawing.Point(0, 0);
        this.btnAnalyzeRules.Location = new System.Drawing.Point(0, 63);
        this.btnAnalyzeRules.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        this.btnClearRules.Location = new System.Drawing.Point(83, 63);
        this.btnClearRules.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
        // lblSearchEngine
        this.lblSearchEngine.AutoSize = true;
        this.lblSearchEngine.Margin = new System.Windows.Forms.Padding(6);
        this.lblSearchEngine.Name = "lblSearchEngine";
        this.lblSearchEngine.TabIndex = 28;
        this.lblSearchEngine.Text = "Search Engine";
        // comboSearchEngine
        this.comboSearchEngine.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.comboSearchEngine.FormattingEnabled = true;
        this.comboSearchEngine.Items.AddRange(new object[] { "Google", "Bing", "Baidu", "DuckDuckGo" });
        this.comboSearchEngine.Name = "comboSearchEngine";
        this.comboSearchEngine.Size = new System.Drawing.Size(121, 21);
        this.comboSearchEngine.TabIndex = 29;
        // tableLayoutPanel
        tableLayoutPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        tableLayoutPanel.ColumnCount = 2;
        tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 44f));
        tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 56f));
        tableLayoutPanel.Controls.Add(this.label1, 0, 0);
        tableLayoutPanel.Controls.Add(this.checkEnableCompleted, 1, 0);
        tableLayoutPanel.Controls.Add(this.label2, 0, 1);
        tableLayoutPanel.Controls.Add(this.checkEnabledHidden, 1, 1);
        tableLayoutPanel.Controls.Add(this.label5, 0, 2);
        tableLayoutPanel.Controls.Add(this.checkReplaceLocalizedNames, 1, 2);
        tableLayoutPanel.Controls.Add(this.lblLanguage, 0, 3);
        tableLayoutPanel.Controls.Add(this.comboLanguage, 1, 3);
        tableLayoutPanel.Controls.Add(this.label4, 0, 4);
        tableLayoutPanel.Controls.Add(this.textPassThroughArgs, 1, 4);
        tableLayoutPanel.Controls.Add(this.label6, 0, 5);
        tableLayoutPanel.Controls.Add(panel, 1, 5);
        tableLayoutPanel.Controls.Add(this.lblAutoStart, 0, 6);
        tableLayoutPanel.Controls.Add(this.panelAutoStartSub, 1, 6);
        tableLayoutPanel.Controls.Add(this.lblAiApiUrl, 0, 7);
        tableLayoutPanel.Controls.Add(this.textAiApiUrl, 1, 7);
        tableLayoutPanel.Controls.Add(this.lblAiApiKey, 0, 8);
        tableLayoutPanel.Controls.Add(this.textAiApiKey, 1, 8);
        tableLayoutPanel.Controls.Add(this.lblAiTargetLanguage, 0, 9);
        tableLayoutPanel.Controls.Add(this.comboAiTargetLanguage, 1, 9);
        tableLayoutPanel.Controls.Add(this.lblAiModel, 0, 10);
        tableLayoutPanel.Controls.Add(panelModel, 1, 10);
        tableLayoutPanel.Controls.Add(this.lblSearchEngine, 0, 11);
        tableLayoutPanel.Controls.Add(this.comboSearchEngine, 1, 11);
        tableLayoutPanel.Controls.Add(this.lblCustomSystemPrompt, 0, 12);
        tableLayoutPanel.Controls.Add(panelPrompt, 1, 12);
        tableLayoutPanel.Controls.Add(this.lblCustomUserMessage, 0, 13);
        tableLayoutPanel.Controls.Add(panelUserMsg, 1, 13);
        tableLayoutPanel.Controls.Add(this.lblTranslationRules, 0, 14);
        tableLayoutPanel.Controls.Add(panelRules, 1, 14);
        tableLayoutPanel.Location = new System.Drawing.Point(9, 9);
        tableLayoutPanel.Margin = new System.Windows.Forms.Padding(0);
        tableLayoutPanel.Name = "tableLayoutPanel1";
        tableLayoutPanel.RowCount = 15;
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27f));
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27f));
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27f));
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27f));
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27f));
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 110f));
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80f));
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 90f));
        tableLayoutPanel.Size = new System.Drawing.Size(395, 720);
        tableLayoutPanel.TabIndex = 7;
        // btnCancel
        button3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        button3.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        button3.Location = new System.Drawing.Point(106, 735);
        button3.Name = "btnCancel";
        button3.Size = new System.Drawing.Size(88, 30);
        button3.TabIndex = 8;
        button3.Text = "Cancel";
        button3.UseVisualStyleBackColor = true;
        button3.Click += new System.EventHandler(btnCancel_Click);
        // Form
        base.AcceptButton = button;
        base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        base.CancelButton = button3;
        base.ClientSize = new System.Drawing.Size(413, 770);
        base.ControlBox = false;
        base.Controls.Add(button3);
        base.Controls.Add(tableLayoutPanel);
        base.Controls.Add(button);
        base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        base.Margin = new System.Windows.Forms.Padding(6);
        base.MaximizeBox = false;
        base.MinimizeBox = false;
        base.Name = "SettingsDlg";
        base.ShowIcon = false;
        base.ShowInTaskbar = false;
        base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Settings";
        base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(SettingsDlg_FormClosing);
        base.Load += new System.EventHandler(Settings_Load);
        tableLayoutPanel.ResumeLayout(false);
        tableLayoutPanel.PerformLayout();
        panel.ResumeLayout(false);
        panel.PerformLayout();
        this.panelAutoStartSub.ResumeLayout(false);
        this.panelAutoStartSub.PerformLayout();
        panelModel.ResumeLayout(false);
        panelModel.PerformLayout();
        panelPrompt.ResumeLayout(false);
        panelPrompt.PerformLayout();
        panelUserMsg.ResumeLayout(false);
        panelUserMsg.PerformLayout();
        panelRules.ResumeLayout(false);
        panelRules.PerformLayout();
        base.ResumeLayout(false);
    }
}
