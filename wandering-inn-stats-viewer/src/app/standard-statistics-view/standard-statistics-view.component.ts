import {Component, Input, OnInit} from '@angular/core';
import {StandardStatisticInterpretation, StatisticsService} from "../statistics.service";

@Component({
  selector: 'app-standard-statistics-view',
  templateUrl: './standard-statistics-view.component.html',
  styleUrls: ['./standard-statistics-view.component.scss']
})
export class StandardStatisticsViewComponent implements OnInit {

  @Input()
  standardStatistics: StandardStatisticInterpretation | null = null;

  constructor() {
  }

  ngOnInit(): void {
  }

}
