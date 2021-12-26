import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  Input,
  OnChanges,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import bb, {Chart, ChartOptions} from "billboard.js";

@Component({
  selector: 'app-smart-chart',
  templateUrl: './smart-chart.component.html',
  styleUrls: ['./smart-chart.component.scss']
})
export class SmartChartComponent implements AfterViewInit, OnChanges {
  @Input()
  public minWidth?: number | null = null;

  @Input()
  public options?: ChartOptions;

  @ViewChild('chartTarget')
  chartTarget!: ElementRef;

  public chart?: Chart;

  resizeTimeout?: number;

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    if (this.minWidth && this.chart) {
      let nativeElement = <HTMLDivElement>this.chartTarget.nativeElement;

      this.resizeTimeout && window.clearTimeout(this.resizeTimeout);
      this.resizeTimeout = window.setTimeout(() => {
        this.chart!.resize({
          width: nativeElement.getBoundingClientRect().width,
          height: nativeElement.getBoundingClientRect().height,
        });
      }, 300);
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    let optionsChange = changes["options"];
    if (optionsChange && !optionsChange.isFirstChange() && optionsChange.currentValue && optionsChange.currentValue != optionsChange.previousValue)
    {
      this.reInit();
    }
  }

  constructor(private hostElementRef: ElementRef) {
  }

  ngAfterViewInit() {
    this.init();
  }

  private reInit(){
    this.chart?.destroy();
    this.init();
  }

  private init() {
    let hostNativeElement = <HTMLDivElement>this.hostElementRef.nativeElement;
    let targetElement = <HTMLDivElement>this.chartTarget.nativeElement;


    if (!this.options)
      throw Error("chart options are undefined")

    let options = this.options;
    options.bindto = targetElement;

    if (this.minWidth) {
      hostNativeElement.classList.add("breakout")
      hostNativeElement.style.setProperty("--breakout", `${this.minWidth}px`);

      if (!hostNativeElement.parentElement?.classList.contains("row")) {
        throw Error("parent does not exist or has no row class")
      }

      hostNativeElement.parentElement.classList.add("breakout")
      this.options.resize = {
        auto: false
      }

      this.options.size = {
        width: targetElement.getBoundingClientRect().width
      }
    }

    this.chart = bb.generate(options);

    window.setTimeout(() => {
      this.onResize(null);
    }, 50)
  }

}
