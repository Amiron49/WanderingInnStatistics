import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {PrettyPrintNumberPipe} from './pretty-print-number.pipe';

/** A big number with a caption. */
@Component({
  selector: 'app-chonker-number-info',
  templateUrl: './chonker-number-info.component.html',
  styleUrls: ['./chonker-number-info.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PrettyPrintNumberPipe]
})
export class ChonkerNumberInfoComponent {
  readonly amount = input.required<number>();
  readonly text = input<string | null>(null);
  readonly subText = input<string | null>(null);
}
