import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {VolumeStatistic} from "../../wandering-inn-statistic";
import {ChartOptions, radar} from "billboard.js";
import {Helpers} from "../main-statistics.component";
import {SmartChartComponent} from "../smart-chart/smart-chart.component";

/** Radar of the five most mentioned characters of a volume. */
@Component({
  selector: 'app-volume-character-occurence-radar',
  templateUrl: './volume-character-occurence-radar.component.html',
  styleUrls: ['./volume-character-occurence-radar.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SmartChartComponent]
})
export class VolumeCharacterOccurenceRadarComponent {
  readonly volumeStatistic = input.required<VolumeStatistic>();
  readonly filterErin = input<boolean>(false);
  /** Shows each character's share of the top five instead of absolute mentions. */
  readonly weighted = input<boolean>(false);
  /** Shows the volume's name above the chart; off where the page already says which volume it is. */
  readonly showLabel = input<boolean>(true);

  readonly chartOptions = computed(() =>
    VolumeCharacterOccurenceRadarComponent.createChartOptions(this.volumeStatistic(), this.filterErin(), this.weighted()));

  private static createChartOptions(statistics: VolumeStatistic, filterErin: boolean, weighted: boolean): ChartOptions {
    const data = VolumeCharacterOccurenceRadarComponent.computeData(statistics, filterErin, weighted);
    const biggestValue = Math.max(...<number[]>data[1].slice(1, data[1].length - 1));

    return {
      size: {
        height: 320,
        width: 550
      },
      data: {
        x: "x",
        columns: <any>data,
        type: radar(),
        labels: {
          colors: "white",
          centered: true,
          format: v => {
            const formatter = new Intl.NumberFormat("en-US", {
              maximumFractionDigits: 2
            });
            return `${formatter.format(v)}`
          }
        }
      },
      radar: {
        axis: {
          max: biggestValue,
          line: {
            show: true
          }
        },
        level: {
          depth: 4
        },
        direction: {
          clockwise: true
        }
      },
      legend: {
        show: false
      },
      resize: {
        auto: true
      }
    };
  }

  private static computeData(statistics: VolumeStatistic, filterErin: boolean, weighted: boolean): (string | number)[][] {
    const topCharacters = Helpers.ToDictionary(statistics.CharacterMentions)
      .filter(Helpers.hackyFilter)
      .filter(x => !filterErin || x.key != "Erin Solstice")
      .sort(Helpers.compare)
      .slice(0, 5);

    let topData = topCharacters.map(x => x.value);

    if (weighted) {
      const totalSum = topData.reduce((acc: number, val) => acc + val);
      topData = topData.map(x => x / totalSum);
    }

    return [
      ["x", ...topCharacters.map(x => x.key.split(" ")[0])],
      ["Mentions", ...topData]
    ]
  }
}
