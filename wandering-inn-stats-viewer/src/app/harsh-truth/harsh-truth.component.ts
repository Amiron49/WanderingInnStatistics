import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

@Component({
    selector: 'app-harsh-truth',
    templateUrl: './harsh-truth.component.html',
    styleUrls: ['./harsh-truth.component.scss'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class HarshTruthComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

}
