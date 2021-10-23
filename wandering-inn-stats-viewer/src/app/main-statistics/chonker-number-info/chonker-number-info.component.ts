import {Component, Input, OnInit} from '@angular/core';

@Component({
  selector: 'app-chonker-number-info',
  templateUrl: './chonker-number-info.component.html',
  styleUrls: ['./chonker-number-info.component.scss']
})
export class ChonkerNumberInfoComponent implements OnInit {

  @Input()
  public amount!: number;
  @Input()
  public text: string | null = null;

  @Input()
  public subText: string | null = null;

  constructor() { }

  ngOnInit(): void {
  }

}
