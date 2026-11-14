import {ChangeDetectionStrategy, Component, inject, output} from '@angular/core';
import {StatisticsService} from "../statistics.service";
import {MatDivider, MatListItem, MatListSubheaderCssMatStyler, MatNavList} from '@angular/material/list';
import {RouterLink} from '@angular/router';

@Component({
  selector: 'app-navigation',
  templateUrl: './app-navigation.component.html',
  styleUrls: ['./app-navigation.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatNavList, MatListItem, RouterLink, MatListSubheaderCssMatStyler, MatDivider]
})
export class AppNavigationComponent {
  readonly onClickNavigationItem = output<void>();
  // Some exported volume names end in a newline; the links and the volume page use the trimmed name.
  readonly volumes = inject(StatisticsService).getStatistics().Volumes.map(x => x.Name.trim());
}
