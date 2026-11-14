import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {OtherBooksComparision, StandardStatisticInterpretation, StatisticsService} from "../statistics.service";
import {VolumeStatistic, WanderingInnStatistic} from "../wandering-inn-statistic";
import {bar, ChartOptions, line} from "billboard.js"
import {MatTab, MatTabGroup} from "@angular/material/tabs";
import {MatCheckbox} from "@angular/material/checkbox";
import {ChonkerNumberInfoComponent} from './chonker-number-info/chonker-number-info.component';
import {SmartChartComponent} from './smart-chart/smart-chart.component';
import {VolumeCharacterOccurenceRadarComponent} from './volume-character-occurence-radar/volume-character-occurence-radar.component';
import {StandardStatisticsViewComponent} from '../standard-statistics-view/standard-statistics-view.component';

@Component({
  selector: 'app-main-statistics',
  templateUrl: './main-statistics.component.html',
  styleUrls: ['./main-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChonkerNumberInfoComponent, SmartChartComponent, MatCheckbox, MatTabGroup, MatTab, VolumeCharacterOccurenceRadarComponent, StandardStatisticsViewComponent]
})
export class MainStatisticsComponent {
  private readonly statisticsService = inject(StatisticsService);

  readonly statistics: WanderingInnStatistic = this.statisticsService.getStatistics();
  readonly lastVolume: VolumeStatistic = this.statistics.Volumes[this.statistics.Volumes.length - 1];
  readonly volumes = this.statistics.Volumes.length;
  readonly chapters = this.statistics.Volumes.flatMap(x => x.Chapters).length;
  readonly standardStatistics = new StandardStatisticInterpretation(this.statistics, this.statisticsService);
  readonly wordCountVsOthersChartOptions = OtherBooksComparision.wordCountVsOthersChartOptions(this.statistics);
  readonly wordCountPerVolumeChartOptions = this.createWordCountPerVolumeChartOptions();

  readonly filterErin = signal(false);
  readonly weighted = signal(false);
  /** The radar tabs: all characters, without Erin, weighted. The tab headers are hidden; the checkboxes pick the tab. */
  readonly selectedTab = computed(() => this.weighted() ? 2 : this.filterErin() ? 1 : 0);

  private createWordCountPerVolumeChartOptions(): ChartOptions {
    let data = [
      ["x", ...this.statistics.Volumes.map(x => x.Name)],
      ["Words", ...this.statistics.Volumes.map(x => x.Words)],
      ["Words Per Day", ...this.statistics.Volumes.map(x => x.WritingVelocity.words.perDay())]
    ]

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: bar(),
        types: {
          "Words Per Day": line()
        },
        axes: {
          "Words": "y",
          "Words Per Day": "y2",
        },
        labels: {
          colors: "white",
          centered: true,
          format: v => {
            let formatter = new Intl.NumberFormat("en-US");
            return `${formatter.format(v)}`
          }
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
          label: "Words",
          tick: {
            //rotate: 67,
            format: (x: number) => {
              let formatter = new Intl.NumberFormat("en-US");
              return `${formatter.format(x / 1000)}K`
            }
          }
        },
        y2: {
          show: true,
          label: "Words per Day"
        },
        x: {
          type: "category"
        }
      },
      legend: {
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

export class Helpers {

  public static hackyFilter(a: KeyValuePair<any, number>):boolean {

    return a.key != "The Putrid One" && a.key != "Goblin Chieftain"
  }

  public static compare(a: KeyValuePair<any, number>, b: KeyValuePair<any, number>) {
    return Helpers.standardCompare(a.value, b.value)
  }

  public static ToDictionary<T>(input: { [key: string]: T }) {
    return Object.entries(input).map(val => {
      return {key: val[0], value: val[1]}
    })
  }

  private static standardCompare(a: number, b: number): number {
    if (a > b)
      return -1;
    else if (a < b)
      return 1;
    return 0;
  }

}

export interface KeyValuePair<T1, T2> {
  key: T1;
  value: T2;
}


