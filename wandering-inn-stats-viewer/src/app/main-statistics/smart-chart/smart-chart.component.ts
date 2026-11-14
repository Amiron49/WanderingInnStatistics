import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  untracked,
  viewChild
} from '@angular/core';
import bb, {Chart, ChartOptions} from "billboard.js";

/** Renders a billboard.js chart and re-renders it whenever its options change. */
@Component({
  selector: 'app-smart-chart',
  templateUrl: './smart-chart.component.html',
  styleUrls: ['./smart-chart.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {'(window:resize)': 'onResize()'}
})
export class SmartChartComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** When set, the chart keeps at least this width and its row scrolls horizontally. */
  readonly minWidth = input<number | null>(null);
  readonly options = input.required<ChartOptions>();

  private readonly chartTarget = viewChild.required<ElementRef<HTMLDivElement>>('chartTarget');

  private chart?: Chart;
  private resizeTimeout?: number;

  constructor() {
    afterRenderEffect(() => {
      const options = this.options();
      const minWidth = this.minWidth();
      untracked(() => this.render(options, minWidth));
    });

    inject(DestroyRef).onDestroy(() => {
      window.clearTimeout(this.resizeTimeout);
      this.chart?.destroy();
    });
  }

  onResize() {
    if (!this.minWidth() || !this.chart)
      return;

    const target = this.chartTarget().nativeElement;
    window.clearTimeout(this.resizeTimeout);
    this.resizeTimeout = window.setTimeout(() => {
      const bounds = target.getBoundingClientRect();
      this.chart?.resize({width: bounds.width, height: bounds.height});
    }, 300);
  }

  private render(options: ChartOptions, minWidth: number | null) {
    this.chart?.destroy();

    const host = this.host.nativeElement;
    const target = this.chartTarget().nativeElement;
    const config: ChartOptions = {...options, bindto: target};

    if (minWidth) {
      host.classList.add("breakout");
      host.style.setProperty("--breakout", `${minWidth}px`);

      if (!host.parentElement?.classList.contains("row"))
        throw Error("parent does not exist or has no row class");

      host.parentElement.classList.add("breakout");
      config.resize = {auto: false};
      config.size = {width: target.getBoundingClientRect().width};
    }

    this.chart = bb.generate(config);
    window.setTimeout(() => this.onResize(), 50);
  }
}
