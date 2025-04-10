using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using pInvestors.Data;
using pInvestors.Models;
using System.Buffers.Text;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace pInvestors.Controllers;

public class OpenAImController : Controller
{
    private readonly CAzureOpenAIUtils _openAIUtils;
    private readonly pInvestorsContext _context;
    private readonly MyLog3 _myLog;

    public OpenAImController(MyLog3 myLog)
    {
        _openAIUtils = new CAzureOpenAIUtils();
        _myLog = myLog;

        if (_myLog.status < 2)
        {
            RedirectToAction("LogFailed", "OpenAI");
            return;
        }
        _context = new pInvestorsContext(_myLog.connectionString);
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (_myLog.status < 2)
            return View("LogFailed");
        return View("Index");
    }


    /// <summary>
    /// The function returns the answer from AI to the given userQuestion. It triggers 
    /// different types of actions depending on askType. 
    /// You need to use an Azure account and a configured OpenAI service.
    /// </summary>
    /// <param name="userQuestion"></param>
    /// <param name="askType"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> AskQuestionAjax(string userQuestion, int askType)
    {
        string systemText = "You are a helpful assistant.";
        var messList = new List<ChatMessage>();
        ChatCompletionOptions options;

        // -----(2) Ask about Function Calling -----------
        if (askType == 2)
        {
            options = new ChatCompletionOptions
            {
                Temperature = 0.0f,
                MaxOutputTokenCount = 400
            };

            string contextText = _context._sqlCreateShareholdersTable;

            if (!string.IsNullOrEmpty(contextText))
                systemText =
                    $" You are an assistant translating user questions into safe SQL SELECT queries" +
                    $" When answering the user, rely on this information.";
        }
        // -----Other case-----------
        else
        {
            options = new ChatCompletionOptions { MaxOutputTokenCount = 800 };
        }

        // ✏️ Prepare messages
        messList.Add(new SystemChatMessage(systemText));
        messList.Add(new UserChatMessage(userQuestion));

        // Send to Azure OpenAI
        string answer = await _openAIUtils.AskHistoryQuestion(messList, options);

        // Return result
        return Json(new { aiAnswer = answer });
    }


    /// <summary>
    /// The function constructs the SQL query based on the query proposal, 
    /// sends it to the database and returns the list from the database in JSON form.
    /// </summary>
    /// <param name="sqlProposal"></param>
    /// <returns>List JSON</returns>
    [HttpGet]
    public async Task<IActionResult> GetShareholdersBySql([FromQuery] string sqlProposal)
    {
        // creates a SQL query based on the proposal
        // The query proposal cannot be executed for security reasons. Therefore,
        // the GetSQLCreateFromProposal function analyzes the query, extracts parameters from it
        // and creates a secure SQL query based on them.
        string sqlQuery = _openAIUtils.GetSQLCreateFromProposal(sqlProposal);

        try
            {
                var list = await _context.Shareholders
                .FromSqlRaw(sqlQuery)
                .ToListAsync();
                //return Json(list);
            return Json(new { success = true, data = list });
        }
        catch (Exception e)
        {
            return Json(new { success = false, error = e.Message });
        }
    }

}
