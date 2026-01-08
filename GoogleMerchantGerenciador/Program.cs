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
//logger.Info("começando");
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
        logger.Error($"{item.idFilaProdutoFeedHistorico} - {item.Sku}: {e.Message}");
    }
}

return;

async Task Proccessa(FilaProdutoFeed filaProdutoFeed, Actions actions)
{
    switch (filaProdutoFeed.Acao)
    {
        case 0:
            await actions.ProcessaApaga(filaProdutoFeed.Sku);
            break;
        case 1:
        case 2:
            await actions.InsereOuAtualiza(filaProdutoFeed.Sku);
            break;
    }
}