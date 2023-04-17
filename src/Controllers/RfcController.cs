using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;

namespace rfc_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RfcController : ControllerBase
{
    private const string sections_qry = "/html/body/div[@id='pageContent']/form//ul[starts-with(@id,'ubicacionForm')]";
    private const string rows_qry = "li[2]/table/tbody/tr/td/div/div/table/tbody/tr[@role='row']";
    private const string keys_qry = "td[@role='gridcell'][1]";
    private const string values_qry = "td[@role='gridcell'][2]";
    private const string date_pattern = @"\d{2}-\d{2}-\d{4}";

    [HttpGet]
    public async Task<object> GetDataByUrl(string url)
    {
        using (HttpClient client = new HttpClient())
        {
            var uri = new Uri(url);

            var response = await client.GetAsync(uri);

            var content = await response.Content.ReadAsStringAsync();

            content = content.Replace("<meta content=\"text/html; charset=UTF-8\" http-equiv=\"Content-Type\" />", string.Empty);

            var doc = XDocument.Parse(content);

            var sections = doc.XPathSelectElements(sections_qry).ToList();

            var dictionaries = new List<Dictionary<string, dynamic>>();
            var activities = new List<Dictionary<string, dynamic>>();

            var query_params = HttpUtility.ParseQueryString(uri.Query);
            var d3 = query_params["D3"];

            var rfc = d3?.Split('=')
                .Last()
                .Split('_')
                .Last();

            var idCIF = d3?.Split('=')
                .Last()
                .Split('_')
                .First();

            foreach (var section in sections.Skip(1).Take(2))
            {
                var dictionary = new Dictionary<string, dynamic>();

                foreach (var row in section.XPathSelectElements(rows_qry).ToList())
                {
                    if (row != null)
                    {
                        var key = ProcessKeyName(row.XPathSelectElement(keys_qry)?.Value);
                        var value = ProcessValue(row.XPathSelectElement(values_qry)?.Value.ReplaceLineEndings());

                        if (key != null && value != null)
                        {
                            dictionary.Add(key, value);
                        }
                    }
                }

                dictionaries.Add(dictionary);
            }

            foreach (var row in sections[3].XPathSelectElements(rows_qry).ToList())
            {
                var dictionary = new Dictionary<string, dynamic>();

                if (row != null)
                {
                    var key = ProcessKeyName(row.XPathSelectElement(keys_qry)?.Value);
                    var value = ProcessValue(row.XPathSelectElement(values_qry)?.Value.ReplaceLineEndings());

                    if (key != null && value != null)
                    {
                        dictionary.Add(key, value);
                    }
                }

                if (activities.Count() == 0 || activities.Last().Count == 2)
                {
                    activities.Add(dictionary);
                }
                else
                {
                    activities.Last().Add(dictionary.First().Key, dictionary.First().Value);
                }
            }

            if (rfc != null && idCIF != null)
            {
                dictionaries.First().Add("CIF", idCIF);
                dictionaries.First().Add("RFC", rfc);
            }

            var result = new
            {
                Identidad = dictionaries[0],
                Ubicacion = dictionaries[1],
                Actividades = activities
            };

            return result;
        }
    }

    [HttpGet]
    [Route("{rfc}/{cif}")]
    public Task<object> GetDataByRfc(string rfc, string cif)
    {
        var url = "https://siat.sat.gob.mx/app/qr/faces/pages/mobile/validadorqr.jsf";

        var query_params = new Dictionary<string, string?>(){
            {"D1","10"},
            {"D2","1"},
            {"D3", $"{cif}_{rfc}"}
        };

        return GetDataByUrl(QueryHelpers.AddQueryString(url, query_params));
    }

    private string? ProcessKeyName(string? key)
    {
        if (key == null)
            return null;

        var result = key
            .ReplaceLineEndings()
            .Trim(':')
            .Replace('á', 'a')
            .Replace('é', 'e')
            .Replace('í', 'i')
            .Replace('ó', 'o')
            .Replace('ú', 'u');

        if (result.Contains(" "))
        {
            return string.Join(string.Empty, result.Split(' ')
                .Where(r => r.Length > 3)
                .Select(r => char.ToUpperInvariant(r[0]).ToString() + r.Substring(1)));
        }
        else
        {
            return result;
        }
    }

    private object? ProcessValue(string? value)
    {
        if (value == null)
            return null;

        var regex = new Regex(date_pattern);

        if (regex.IsMatch(value))
        {
            return DateTime.ParseExact(value, "dd-MM-yyyy", null);
        }

        return value;

    }
}
