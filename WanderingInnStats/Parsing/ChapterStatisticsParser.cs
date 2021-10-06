using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using WanderingInnStats.Core;

namespace WanderingInnStats.Parsing
{
    public class ChapterStatisticsParser
    {
        private readonly ILogger _logger;
        private readonly WanderingInnDefinitions _definitions;

        private readonly ITextParser[] _parsers;

        public ChapterStatisticsParser(ILogger logger, WanderingInnDefinitions definitions)
        {
            _logger = logger;
            _definitions = definitions;

            _parsers = new ITextParser[]
            {
                new BracketsParser(_logger),
                new SimpleWordStats(),
                new CharacterStats()
            };
        }

        public async Task<List<(Chapter Chapter, IWanderingInnStatistics Statistics)>> Create(IEnumerable<Chapter> chapters)
        {
            var chaptersAsAsync = chapters.Select(async chapter =>
            {
                _logger.LogInformation("Parsing: {chapter}", chapter.Name);
                var statistics = await Create(chapter);
                return (chapter, statistics);
            });

            var result = await Task.WhenAll(chaptersAsAsync);

            return result.ToList();
        }

        public Task<IWanderingInnStatistics> Create(Chapter chapter)
        {
            return Task.Run(() =>
            {
                var stats = new WanderingInnStatistics();

                foreach (var parser in _parsers)
                {
                    parser.Parse(chapter.Text, stats, _definitions);
                }

                return (IWanderingInnStatistics)stats;
            });
        }
    }

    public static class AsyncHelper
    {
        public static async Task<List<T>> ToListAsyncInParallel<T>(this IAsyncEnumerable<T> enumerable, int maxConcurrent = 24)
        {
            var result = new ConcurrentBag<T>();

            await enumerable.AsyncParallelForEach(async arg =>
            {
                await Task.Run(() => result.Add(arg));
            }, maxConcurrent);

            return result.ToList();
        }

        public static async Task ForEachInParallel<T>(this IAsyncEnumerable<T> enumerable, Func<T, Task> action, int maxConcurrent = 24)
        {
            await enumerable.AsyncParallelForEach(async arg => { await action(arg); }, maxConcurrent);
        }

        private static async Task AsyncParallelForEach<T>(this IAsyncEnumerable<T> source, Func<T, Task> body,
            int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded)
        {
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
            var block = new ActionBlock<T>(body, options);
            await foreach (var item in source)
                block.Post(item);
            block.Complete();
            await block.Completion;
        }
    }
}