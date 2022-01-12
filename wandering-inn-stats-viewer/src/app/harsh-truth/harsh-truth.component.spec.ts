import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HarshTruthComponent } from './harsh-truth.component';

describe('HarshTruthComponent', () => {
  let component: HarshTruthComponent;
  let fixture: ComponentFixture<HarshTruthComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ HarshTruthComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(HarshTruthComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
