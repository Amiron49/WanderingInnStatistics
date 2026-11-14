import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

@Component({
    selector: 'app-impressum',
    templateUrl: './impressum.component.html',
    styleUrls: ['./impressum.component.scss'],
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class ImpressumComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

}
