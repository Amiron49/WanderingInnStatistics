import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {StandardStatisticInterpretation} from "../statistics.service";
import {ChonkerNumberInfoComponent} from '../main-statistics/chonker-number-info/chonker-number-info.component';
import {SmartChartComponent} from '../main-statistics/smart-chart/smart-chart.component';

@Component({
  selector: 'app-standard-statistics-view',
  templateUrl: './standard-statistics-view.component.html',
  styleUrls: ['./standard-statistics-view.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChonkerNumberInfoComponent, SmartChartComponent]
})
export class StandardStatisticsViewComponent {
  readonly standardStatistics = input<StandardStatisticInterpretation | null>(null);
}
