using ControllerDBApi;
using ControllerDBApi.GoogleFeeds;
using GoogleMerchantGerenciador;
using NLog;

var logger = LogManager.GetCurrentClassLogger();
var db = new ControllerDB();
var actions = new Actions(db);

var queue = await db.GoogleFeed.GetFeedQueue();

foreach (var item in queue)
{
    try
    {
        await Proccessa(item, actions);
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
        1 => actions.ProcessaInsere(filaProdutoFeed.Sku),
        2 => actions.ProcessaAtualiza(filaProdutoFeed.Sku),
        _ => throw new ArgumentOutOfRangeException()
    };
}