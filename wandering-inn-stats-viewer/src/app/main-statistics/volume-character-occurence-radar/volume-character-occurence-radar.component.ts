import {Component, Input, OnChanges, OnInit, SimpleChange, SimpleChanges, ViewChild} from '@angular/core';
import {VolumeStatistic} from "../../wandering-inn-statistic";
import {ChartOptions, radar} from "billboard.js";
import {Helpers} from "../main-statistics.component";
import {SmartChartComponent} from "../smart-chart/smart-chart.component";

@Component({
  selector: 'app-volume-character-occurence-radar',
  templateUrl: './volume-character-occurence-radar.component.html',
  styleUrls: ['./volume-character-occurence-radar.component.scss']
})
export class VolumeCharacterOccurenceRadarComponent implements OnInit, OnChanges {

  @Input()
  volumeStatistic?: VolumeStatistic;
  @Input()
  filterErin: boolean = false;
  @Input()
  weighted: boolean = false;

  @ViewChild(SmartChartComponent)
  smartChart!: SmartChartComponent;

  constructor() {
  }

  ngOnInit(): void {
  }

  ngOnChanges(changes: SimpleChanges) {
    let erinChange = changes["filterErin"];
    let weightChange = changes["weighted"];

    if (Helpers.isChange(erinChange) || Helpers.isChange(weightChange)) {
      this.recalculate(erinChange.currentValue, weightChange.currentValue);
    }
  }

  recalculate(filterErin: boolean, weighted: boolean) {
    let data = this.computeData(this.volumeStatistic!, filterErin, weighted);
    this.smartChart.chart?.load({
      data: <any>data
    });

    let biggestValue = Math.max(...<number[]>data[1].splice(0, 1));

    if (weighted && this.smartChart.chart!.axis.max() > 2) {
      this.smartChart.chart!.axis.max(1);
    } else if (!weighted && this.smartChart.chart!.axis.max() < 2) {
      this.smartChart.chart!.axis.max(biggestValue);
    }
  }

  createVolumeCharacterMentionsChartOptions(): ChartOptions {
    let data = this.computeData(this.volumeStatistic!, this.filterErin, this.weighted)

    let biggestValue = Math.max(...<number[]>data[1].splice(0, 1));

    return {
      data: {
        x: "x",
        columns: <any>data,
        type: radar(),
        labels: {
          colors: "white",
          centered: true
        }
      },
      radar: {
        axis: {
          max: biggestValue,
          text: {
            // position: {
            //   x: -50
            // }
          },
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
      }
    };
  }

  computeData(statistics: VolumeStatistic, filterErin: boolean, weighted: boolean): (string | number)[][] {
    let topCharacters = Helpers.ToDictionary(statistics.CharacterMentions).filter(Helpers.hackyFilter).filter(x => {
      if (!filterErin)
        return true;

      return x.key != "Erin Solstice";
    }).sort(Helpers.compare).slice(0, 5);

    let topData = topCharacters.map(x => x.value);

    if (weighted) {
      let totalSum = topData.reduce((acc: number, val) => {
        return acc + val
      });
      topData = topData.map(x => x / totalSum);
    }

    return [
      ["x", ...topCharacters.map(x => x.key)],
      ["Mentions", ...topData]
    ]
  }
}
