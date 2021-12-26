import {
  AfterViewInit,
  Component,
  Input,
  OnChanges,
  OnInit,
  SimpleChange,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import {VolumeStatistic} from "../../wandering-inn-statistic";
import {ChartOptions, radar} from "billboard.js";
import {Helpers} from "../main-statistics.component";
import {SmartChartComponent} from "../smart-chart/smart-chart.component";
import {NumberFormatStyle} from "@angular/common";

@Component({
  selector: 'app-volume-character-occurence-radar',
  templateUrl: './volume-character-occurence-radar.component.html',
  styleUrls: ['./volume-character-occurence-radar.component.scss']
})
export class VolumeCharacterOccurenceRadarComponent implements OnInit, OnChanges, AfterViewInit {

  @Input()
  volumeStatistic?: VolumeStatistic;
  @Input()
  filterErin: boolean = false;
  @Input()
  weighted: boolean = false;

  chartOptions?: ChartOptions;

  @ViewChild(SmartChartComponent)
  smartChart!: SmartChartComponent;

  constructor() {
  }

  ngOnInit(): void {
  }

  ngAfterViewInit() {
    this.chartOptions = this.createVolumeCharacterMentionsChartOptions();
  }

  ngOnChanges(changes: SimpleChanges) {
    let erinChange = changes["filterErin"];
    let weightChange = changes["weighted"];

    if (Helpers.isChange(erinChange) || Helpers.isChange(weightChange)) {
      this.recalculate(erinChange?.currentValue ?? this.filterErin, weightChange?.currentValue ?? this.weighted);
    }
  }

  recalculate(filterErin: boolean, weighted: boolean) {
    let data = this.computeData(this.volumeStatistic!, filterErin, weighted);
    this.smartChart.chart?.load({
      columns: <any>data
    });

    let biggestValue = Math.max(...<number[]>data[1].slice(1, data[1].length - 1));

    if (weighted && this.smartChart.chart!.config("radar.axis.max") > 2) {
      this.smartChart.chart!.config("radar.axis.max", 1, true);
    } else if (!weighted && this.smartChart.chart!.config("radar.axis.max") < 2) {
      this.smartChart.chart!.config("radar.axis.max", biggestValue, true);
    }
  }

  createVolumeCharacterMentionsChartOptions(): ChartOptions {
    let data = this.computeData(this.volumeStatistic!, this.filterErin, this.weighted)

    let biggestValue = Math.max(...<number[]>data[1].slice(1, data[1].length - 1));

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
            let formatter = new Intl.NumberFormat("en-US", {
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
      ["x", ...topCharacters.map(x => x.key.split(" ")[0])],
      ["Mentions", ...topData]
    ]
  }
}
