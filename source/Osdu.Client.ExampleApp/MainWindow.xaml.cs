using System.Text.Json;
using System.Windows;
using Osdu.Client.Apis;
using Osdu.Client.Apis.Search;
using Osdu.Client.Apis.WellboreDdms;
using Osdu.Client.Extensions;
using Osdu.Client.Schemas.MasterData;
using Osdu.Client.Schemas.WorkProductComponent;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IOsduClient _osduClient;

    public MainWindow(IOsduClient osduClient)
    {
        InitializeComponent();
        _osduClient = osduClient;
    }

    //private async void SearchButton_Click(object sender, RoutedEventArgs e)
    //{
    //    SearchButton.IsEnabled = false;
    //    StatusText.Text = "Working...";
    //    ResultsTextBox.Clear();

    //    try
    //    {
    //        QueryRequest request = new QueryRequest
    //        {
    //            Kind = KindTextBox.Text,
    //            Limit = 20,
    //            Query = "*"
    //        };

    //        QueryResponse? response = await _osduClient.Search.PostQueryAsync(request);

    //        IEnumerable<Wellbore_1_3_0> wellbores = response.Results.Deserialize<Wellbore_1_3_0>();

    //        StatusText.Text = $"Found {response.TotalCount} result(s).";

    //        string prettyJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
    //        {
    //            WriteIndented = true
    //        });

    //        ResultsTextBox.Text = prettyJson;
    //    }
    //    catch (Exception ex)
    //    {
    //        ResultsTextBox.Text = $"Error: {ex.Message}";
    //    }
    //    finally
    //    {
    //        SearchButton.IsEnabled = true;
    //    }
    //}

    //private async void SearchButton_Click(object sender, RoutedEventArgs e)
    //{
    //    SearchButton.IsEnabled = false;
    //    StatusText.Text = "Working...";
    //    ResultsTextBox.Clear();

    //    try
    //    {

    //        AboutResponse response = await _osduClient.WellboreDdms.GetAboutAsync(CancellationToken.None);

    //        string prettyJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
    //        {
    //            WriteIndented = true
    //        });

    //        ResultsTextBox.Text = prettyJson;

    //        StatusText.Text = "Done!!!";
    //    }
    //    catch (Exception ex)
    //    {
    //        ResultsTextBox.Text = $"Error: {ex.Message}";
    //    }
    //    finally
    //    {
    //        SearchButton.IsEnabled = true;
    //    }
    //}

    //private async void SearchButton_Click(object sender, RoutedEventArgs e)
    //{
    //    SearchButton.IsEnabled = false;
    //    StatusText.Text = "Working...";
    //    ResultsTextBox.Clear();

    //    try
    //    {
    //        QueryRequest request = new QueryRequest
    //        {
    //            Kind = "osdu:wks:work-product-component--WellLog:*",
    //            Query = "*",
    //            Limit = 10,
    //            ReturnedFields = ["id", "kind", "createTime"],
    //        };

    //        var response = await _osduClient.Search.PostQueryAsync(request);

    //        string prettyJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
    //        {
    //            WriteIndented = true
    //        });

    //        ResultsTextBox.Text = prettyJson;

    //        StatusText.Text = "Done!!!";
    //    }
    //    catch (Exception ex)
    //    {
    //        ResultsTextBox.Text = $"Error: {ex.Message}";
    //    }
    //    finally
    //    {
    //        SearchButton.IsEnabled = true;
    //    }
    //}

    //private async void SearchButton_Click(object sender, RoutedEventArgs e)
    //{
    //    SearchButton.IsEnabled = false;
    //    StatusText.Text = "Working...";
    //    ResultsTextBox.Clear();

    //    try
    //    {
    //        WellLog_1_5_0Data wellLog = new WellLog_1_5_0Data()
    //        {
    //            WellboreID = "dev:master-data--Wellbore:3728af7d649d4df4805d38d38aeae659",
    //            TopMeasuredDepth = 1001.0,
    //            BottomMeasuredDepth = 2001.0,
    //            IsRegular = true,
    //            Curves = [new WellLog_1_5_0DataCurves { CurveID = "GR", Mnemonic = "GR", CurveDescription = "MRK Gamma Ray", NumberOfColumns = 1 }]
    //        };

    //        List<Record> records = new List<Record>()
    //        {
    //            new Record()
    //            {
    //                Kind = "osdu:wks:work-product-component--WellLog:1.5.0",
    //                Acl = new StorageAcl {Viewers = ["data.office.global.viewers@dev.dataservices.energy"], Owners = ["data.wellcoredb.owners@dev.dataservices.energy"]},
    //                Legal = new Legal {Legaltags = ["dev-equinor-private-default"], OtherRelevantDataCountries = ["NO","US"]},
    //                Data = wellLog
    //            }
    //        };

    //        var response = await _osduClient.WellboreDdms.PostDdmsV3WelllogsAsync(records);

    //        string prettyJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
    //        {
    //            WriteIndented = true
    //        });

    //        ResultsTextBox.Text = prettyJson;

    //        StatusText.Text = "Done!!!";
    //    }
    //    catch (Exception ex)
    //    {
    //        ResultsTextBox.Text = $"Error: {ex.Message}";
    //    }
    //    finally
    //    {
    //        SearchButton.IsEnabled = true;
    //    }
    //}

    //private async void SearchButton_Click(object sender, RoutedEventArgs e)
    //{
    //    SearchButton.IsEnabled = false;
    //    StatusText.Text = "Working...";
    //    ResultsTextBox.Clear();

    //    try
    //    {
    //        string wellLogId = "dev:work-product-component--WellLog:1c35012eab4d4c5d90eb38b9fb245522";

    //        var response = await _osduClient.Storage.PostRecordsDeleteByIdAsync(wellLogId);

    //        string prettyJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
    //        {
    //            WriteIndented = true
    //        });

    //        ResultsTextBox.Text = prettyJson;

    //        StatusText.Text = "Done!!!";
    //    }
    //    catch (Exception ex)
    //    {
    //        ResultsTextBox.Text = $"Error: {ex.Message}";
    //    }
    //    finally
    //    {
    //        SearchButton.IsEnabled = true;
    //    }
    //}


    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchButton.IsEnabled = false;
        StatusText.Text = "Working...";
        ResultsTextBox.Clear();

        try
        {
            var response = await _osduClient.Storage.GetRecordsAsync(10);

            string prettyJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            ResultsTextBox.Text = prettyJson;

            StatusText.Text = "Done!!!";
        }
        catch (Exception ex)
        {
            ResultsTextBox.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

}
