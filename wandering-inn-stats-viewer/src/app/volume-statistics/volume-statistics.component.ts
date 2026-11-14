import {ChangeDetectionStrategy, Component, computed, inject, input, signal} from '@angular/core';
import {VolumeStatistic} from "../wandering-inn-statistic";
import {bar, ChartOptions} from "billboard.js";
import {StandardStatisticInterpretation, StatisticsService} from "../statistics.service";
import {MatTab, MatTabGroup} from "@angular/material/tabs";
import {MatCheckbox} from "@angular/material/checkbox";
import {ChonkerNumberInfoComponent} from '../main-statistics/chonker-number-info/chonker-number-info.component';
import {SmartChartComponent} from '../main-statistics/smart-chart/smart-chart.component';
import {VolumeCharacterOccurenceRadarComponent} from '../main-statistics/volume-character-occurence-radar/volume-character-occurence-radar.component';
import {StandardStatisticsViewComponent} from '../standard-statistics-view/standard-statistics-view.component';

@Component({
  selector: 'app-volume-statistics',
  templateUrl: './volume-statistics.component.html',
  styleUrls: ['./volume-statistics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChonkerNumberInfoComponent, SmartChartComponent, MatCheckbox, MatTabGroup, MatTab, VolumeCharacterOccurenceRadarComponent, StandardStatisticsViewComponent]
})
export class VolumeStatisticsComponent {
  private readonly statisticsService = inject(StatisticsService);

  /** Route parameter <c>:volume</c>, the volume's name. */
  readonly volume = input.required<string>();

  readonly statistics = computed(() =>
    this.statisticsService.getStatistics().Volumes.find(x => x.Name.trim() == this.volume().trim()));
  readonly standardStatistics = computed(() => {
    const statistics = this.statistics();
    return statistics ? new StandardStatisticInterpretation(statistics, this.statisticsService) : undefined;
  });
  readonly wordCountPerChapterChartOptions = computed(() => {
    const statistics = this.statistics();
    return statistics ? VolumeStatisticsComponent.createWordCountPerChapterChartOptions(statistics) : undefined;
  });

  readonly filterErin = signal(false);
  readonly weighted = signal(false);
  /** The radar tabs: all characters, without Erin, weighted. The tab headers are hidden; the checkboxes pick the tab. */
  readonly selectedTab = computed(() => this.weighted() ? 2 : this.filterErin() ? 1 : 0);

  private static createWordCountPerChapterChartOptions(volumeStats: VolumeStatistic): ChartOptions {
    let data = [
      ["x", ...volumeStats.Chapters.map(x => x.Chapter.Name)],
      ["Words", ...volumeStats.Chapters.map(x => x.Words)]
    ]

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: bar(),
        axes: {
          "Words": "y"
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
