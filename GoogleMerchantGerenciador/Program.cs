using ControllerDBApi;
using ControllerDBApi.GoogleFeeds;
using GoogleMerchantGerenciador;
using NLog;

var logger = LogManager.GetCurrentClassLogger();
var db = new ControllerDB();
var actions = new Actions(db);

var queue = (await db.GoogleFeed.GetFeedQueue()).ToArray();

var count = 0;
Console.WriteLine(queue.Length);
foreach (var item in queue)
{
    if (count % 10 == 0) Console.WriteLine(count);
    count++;
    try
    {
        await Proccessa(item, actions);
        await db.GoogleFeed.DeleteSkuFromFilaProdutoFeed(item.Sku, item.idFilaProdutoFeedHistorico);
    }
    catch (Exception e)
    {
        logger.Error(e.Message);
    }
}

return;

async Task Proccessa(FilaProdutoFeed filaProdutoFeed, Actions actions)
{
    var r = filaProdutoFeed.Acao switch
    {
        0 => actions.ProcessaApaga(filaProdutoFeed.Sku),
        1 => actions.InsereOuAtualiza(filaProdutoFeed.Sku),
        2 => actions.InsereOuAtualiza(filaProdutoFeed.Sku),
        _ => throw new ArgumentOutOfRangeException()
    };
}