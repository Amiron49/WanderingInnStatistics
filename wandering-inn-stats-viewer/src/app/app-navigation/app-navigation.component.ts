import {Component, EventEmitter, OnInit, Output} from '@angular/core';
import {StatisticsService} from "../statistics.service";

@Component({
  selector: 'app-navigation',
  templateUrl: './app-navigation.component.html',
  styleUrls: ['./app-navigation.component.scss']
})
export class AppNavigationComponent implements OnInit {

  @Output()
  onClickNavigationItem: EventEmitter<void> = new EventEmitter<void>();
  volumes: string[] = [];

  constructor(
    private statisticService: StatisticsService
  ) {
    this.volumes = statisticService.getStatistics().Volumes.map(x => x.Name);
  }

  ngOnInit(): void {
  }

}
