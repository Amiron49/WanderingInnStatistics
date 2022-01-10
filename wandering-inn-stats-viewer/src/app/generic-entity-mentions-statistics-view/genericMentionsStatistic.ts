import {
  AbstractWanderingInnStatistic,
  ChapterInfo, ChapterStatistic,
  WanderingInnStatistic
} from "../wandering-inn-statistic";
import {Helpers, KeyValuePair} from "../main-statistics/main-statistics.component";
import {bar, ChartOptions} from "billboard.js";
import {ArrayDataSource} from "@angular/cdk/collections";

export class GenericMentionsStatistic {
  name: string;
  mentions: number;
  firstOccurence: ChapterInfo;
  allChaptersContaining: ChapterInfo[];
  volumeMentionChart: ChartOptions;
  private readonly locator: (stats: AbstractWanderingInnStatistic) => { [p: string]: number };
  private readonly key: string;

  constructor(wanderingInnStatistics: WanderingInnStatistic, key: string, locator: (stats: AbstractWanderingInnStatistic) => { [key: string]: number }) {
    this.key = key;
    this.name = key;
    this.locator = locator;

    this.mentions = this.locator(wanderingInnStatistics)[key];
    this.allChaptersContaining = wanderingInnStatistics.Volumes.flatMap(volume => volume.Chapters.filter(chapter => this.locator(chapter) && this.locator(chapter)[key] > 0)).map(x => x.Chapter);
    this.firstOccurence = this.allChaptersContaining[0];
    this.volumeMentionChart = this.generateVolumeMentionChartOptions(wanderingInnStatistics)
  }

  private generateVolumeMentionChartOptions(wanderingInnStatistics: WanderingInnStatistic): ChartOptions {

    let perVolume = wanderingInnStatistics.Volumes.map(volume => {
      return {
        x: volume.Name,
        y: this.locator(volume)[this.key] ?? 0
      }})

    let data = [
      ["x", ...perVolume.map(x => x.x)],
      ["Mentions", ...perVolume.map(x => x.y)]
    ]

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: bar(),
        labels: {
          colors: "white",
          centered: true
        }
      },
      grid: {
        y: {
          lines: [
            {
              value: 0
            }
          ],
        }
      },
      axis: {
        y: {
          show: true,
          label: "Mentions"
        },
        x: {
          type: "category",
          label: "Volume"
        }
      },
      legend: {
        // show: true,
        show: false
      },
      bar: {
        width: {
          ratio: 0.5
        }
      }
    };
  }

  private static createKVPMentionBasedBarChart(thingies: KeyValuePair<string, number>[], amount: number, yAxisLabel = "Mentions"): ChartOptions {
    let dataData = thingies.sort(Helpers.compare).slice(0, amount);

    let data = [
      ["x", ...dataData.map(x => x.key)],
      [yAxisLabel, ...dataData.map(x => x.value)]
    ]

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: bar(),
        labels: {
          colors: "white",
          centered: true
        }
      },
      grid: {
        y: {
          lines: [
            {
              value: 0
            }
          ],
        }
      },
      axis: {
        y: {
          show: true,
          label: yAxisLabel
        },
        x: {
          type: "category"
        }
      },
      legend: {
        // show: true,
        show: false
      },
      bar: {
        width: {
          ratio: 0.5
        }
      }
    };
  }
}
