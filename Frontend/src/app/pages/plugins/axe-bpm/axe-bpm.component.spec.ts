import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AxeBpmComponent } from './axe-bpm.component';

describe('AxeBpmComponent', () => {
  let component: AxeBpmComponent;
  let fixture: ComponentFixture<AxeBpmComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AxeBpmComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AxeBpmComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
